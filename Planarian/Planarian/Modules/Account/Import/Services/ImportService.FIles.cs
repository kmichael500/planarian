using Planarian.Library.Exceptions;
using Planarian.Modules.Account.Import.Models;
using Planarian.Shared.Services;

namespace Planarian.Modules.Account.Import.Services;

public partial class ImportService
{
    public async Task<ImportFileUploadSessionVm> CreateFileUploadSession(
        ImportFileRequest request,
        CancellationToken cancellationToken)
    {
        await CleanupExpiredUploadSessions(cancellationToken);

        var validation = await ValidateFileImport(
            request.FileName,
            request.IdRegex,
            request.DelimiterRegex,
            request.IgnoreDuplicates,
            request.RequestId,
            cancellationToken);
        if (!validation.Result.IsSuccessful)
        {
            throw ApiExceptionDictionary.FromErrorCode(
                validation.Result.FailureCode,
                validation.Result.Message,
                validation.Result);
        }

        var session = await _chunkedUploadService.CreateSession(
            new ChunkedUploadSessionCreateRequest
            {
                FileName = request.FileName,
                FileSize = request.FileSize,
                BlobKeyPrefix = "temp/import/file-upload-sessions",
                RequestId = request.RequestId,
                Metadata = new ImportFileUploadSessionMetadata
                {
                    DelimiterRegex = request.DelimiterRegex,
                    IdRegex = request.IdRegex,
                    IgnoreDuplicates = request.IgnoreDuplicates,
                    CompletionRequestId = request.RequestId,
                },
            },
            cancellationToken);

        return ToImportFileUploadSessionVm(session);
    }

    public async Task<ImportFileUploadSessionVm> UploadFileChunk(
        string sessionId,
        Stream chunkStream,
        long offset,
        int chunkIndex,
        long contentLength,
        CancellationToken cancellationToken)
    {
        await CleanupExpiredUploadSessions(cancellationToken);

        var importSession = GetRequiredImportUploadSession(sessionId);
        if (importSession.CompletionResult != null)
        {
            return ToImportFileUploadSessionVm(_chunkedUploadService.GetRequiredSession(sessionId));
        }

        var session = await _chunkedUploadService.UploadChunk(
            sessionId,
            chunkStream,
            offset,
            chunkIndex,
            contentLength,
            cancellationToken);

        return ToImportFileUploadSessionVm(session);
    }

    public async Task<FileImportResult> FinalizeFileUploadSession(string sessionId, CancellationToken cancellationToken)
    {
        await CleanupExpiredUploadSessions(cancellationToken);

        var importSession = GetRequiredImportUploadSession(sessionId);
        if (importSession.CompletionResult != null)
        {
            return importSession.CompletionResult;
        }

        var session = await _chunkedUploadService.CommitSession(sessionId, cancellationToken);

        var result = await ProcessStagedFileImport(
            session.BlobKey,
            session.FileName,
            importSession.IdRegex,
            importSession.DelimiterRegex,
            importSession.IgnoreDuplicates,
            importSession.CompletionRequestId ?? session.SessionId,
            cancellationToken);

        result.RequestId ??= importSession.CompletionRequestId ?? session.SessionId;
        importSession.CompletionResult = result;

        try
        {
            await _chunkedUploadService.DeleteCommittedSessionBlob(sessionId);
        }
        catch
        {
            // The file has already been published successfully. Cleanup failures
            // should not turn the request into a user-visible import failure.
        }

        try
        {
            _chunkedUploadService.ReleaseReservedProcessingSlot(sessionId);
        }
        catch
        {
            // The import succeeded; treat slot-release cleanup as best effort.
        }

        return result;
    }

    public async Task CancelFileUploadSession(string sessionId, CancellationToken cancellationToken)
    {
        await CleanupExpiredUploadSessions(cancellationToken);

        GetRequiredImportUploadSession(sessionId);
        await _chunkedUploadService.CancelSession(sessionId, cancellationToken);
    }

    public async Task CancelActiveFileUploadSessions(CancellationToken cancellationToken)
    {
        await CleanupExpiredUploadSessions(cancellationToken);
        await _chunkedUploadService.CancelActiveSessionsForCurrentUser(cancellationToken);
    }

    private async Task<FileImportResult> ProcessFileImport(Stream stream, string fileName, string idRegex,
        string delimiterRegex, bool ignoreDuplicates, string? requestId, CancellationToken cancellationToken)
    {
        var validation = await ValidateFileImport(
            fileName,
            idRegex,
            delimiterRegex,
            ignoreDuplicates,
            requestId,
            cancellationToken);
        if (!validation.Result.IsSuccessful || string.IsNullOrWhiteSpace(validation.CaveId))
        {
            return validation.Result;
        }

        await _fileService.UploadCaveFile(stream, validation.CaveId, fileName, cancellationToken, requestId);

        return validation.Result;
    }

    private async Task<FileImportResult> ProcessStagedFileImport(string stagedBlobKey, string fileName, string idRegex,
        string delimiterRegex, bool ignoreDuplicates, string? requestId, CancellationToken cancellationToken)
    {
        var validation = await ValidateFileImport(
            fileName,
            idRegex,
            delimiterRegex,
            ignoreDuplicates,
            requestId,
            cancellationToken);
        if (!validation.Result.IsSuccessful || string.IsNullOrWhiteSpace(validation.CaveId))
        {
            return validation.Result;
        }

        await _fileService.PublishStagedCaveFile(stagedBlobKey, validation.CaveId, fileName, cancellationToken,
            requestId);

        return validation.Result;
    }

    private async Task<(FileImportResult Result, string? CaveId)> ValidateFileImport(string fileName, string idRegex,
        string delimiterRegex, bool ignoreDuplicates, string? requestId, CancellationToken cancellationToken)
    {
        var result = new FileImportResult
        {
            FileName = fileName,
            IsSuccessful = true,
            IsRetryable = false,
            RequestId = requestId,
        };

        CountyCaveInfo parsed;
        try
        {
            parsed = CountyCaveInfo.Parse(fileName, idRegex, delimiterRegex);
        }
        catch (ApiException e)
        {
            result.Message = e.Message;
            result.IsSuccessful = false;
            result.FailureCode = e.ErrorCode.ToString();
            return (result, null);
        }

        var caveInformation =
            await _repository.GetCaveForFileImportByCountyCodeNumber(parsed.CountyCode, parsed.CountyCaveNumber,
                cancellationToken);

        if (caveInformation == null)
        {
            result.Message =
                $"Cave with county code {parsed.CountyCode} and number {parsed.CountyCaveNumber} does not exist for file '{fileName}'.";
            result.IsSuccessful = false;
            result.FailureCode = ApiExceptionType.NotFound.ToString();
            return (result, null);
        }
        result.AssociatedCave = caveInformation.CaveName;

        if (ignoreDuplicates)
        {
            var isDuplicate = await _fileRepository.IsDuplicateFile(caveInformation.CaveId, fileName);
            if (isDuplicate)
            {
                result.Message = $"File already exists for cave '{caveInformation.CaveName}'.";
                result.IsSuccessful = false;
                result.FailureCode = ApiExceptionType.Conflict.ToString();
                return (result, null);
            }
        }

        return (result, caveInformation.CaveId);
    }

    private ImportFileUploadSessionMetadata GetRequiredImportUploadSession(string sessionId)
    {
        return _chunkedUploadService.GetRequiredSessionMetadata<ImportFileUploadSessionMetadata>(sessionId);
    }

    private async Task CleanupExpiredUploadSessions(CancellationToken cancellationToken)
    {
        await _chunkedUploadService.CleanupExpiredSessions(cancellationToken);
    }

    private static ImportFileUploadSessionVm ToImportFileUploadSessionVm(ChunkedUploadSessionState session)
    {
        return new ImportFileUploadSessionVm
        {
            SessionId = session.SessionId,
            UploadedBytes = session.UploadedBytes,
            TotalBytes = session.FileSize,
            Status = session.Status.ToString().ToLowerInvariant(),
        };
    }
}
