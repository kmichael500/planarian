using System.Collections.Concurrent;
using Azure.Storage.Blobs.Specialized;
using Planarian.Modules.Account.Model;

namespace Planarian.Modules.Account.Services;

public class ImportFileUploadSessionService
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(2);
    private readonly ConcurrentDictionary<string, ImportFileUploadSessionState> _sessions = new();

    public ImportFileUploadSessionState CreateSession(
        string? accountId,
        string containerName,
        CreateImportFileUploadSessionRequest request)
    {
        var session = new ImportFileUploadSessionState
        {
            SessionId = Guid.NewGuid().ToString("N"),
            AccountId = accountId,
            ContainerName = containerName,
            FileName = request.FileName,
            FileSize = request.FileSize,
            DelimiterRegex = request.DelimiterRegex,
            IdRegex = request.IdRegex,
            IgnoreDuplicates = request.IgnoreDuplicates,
            RequestId = request.RequestId,
            Status = ImportFileUploadSessionStatus.Created,
            ExpiresOn = DateTimeOffset.UtcNow.Add(SessionTtl),
        };

        _sessions[session.SessionId] = session;
        return session;
    }

    public ImportFileUploadSessionState? GetSession(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return null;
        }

        if (session.ExpiresOn <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(sessionId, out _);
            return null;
        }

        return session;
    }

    public IReadOnlyCollection<ImportFileUploadSessionState> RemoveExpiredSessions()
    {
        var expired = new List<ImportFileUploadSessionState>();

        foreach (var (sessionId, session) in _sessions)
        {
            if (session.ExpiresOn > DateTimeOffset.UtcNow)
            {
                continue;
            }

            if (_sessions.TryRemove(sessionId, out var removed))
            {
                expired.Add(removed);
            }
        }

        return expired;
    }

    public void RemoveSession(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}

public class ImportFileUploadSessionState
{
    public string SessionId { get; set; } = null!;
    public string? AccountId { get; set; }
    public string ContainerName { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public long FileSize { get; set; }
    public string DelimiterRegex { get; set; } = null!;
    public string IdRegex { get; set; } = null!;
    public bool IgnoreDuplicates { get; set; }
    public string? RequestId { get; set; }
    public string BlobKey => $"temp/import/file-upload-sessions/{SessionId}";
    public long UploadedBytes { get; set; }
    public int NextChunkIndex { get; set; }
    public ImportFileUploadSessionStatus Status { get; set; }
    public DateTimeOffset ExpiresOn { get; set; }
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public SortedDictionary<int, ImportFileUploadChunkState> UploadedChunks { get; } = new();
    public FileImportResult? CompletionResult { get; set; }

    public ImportFileUploadSessionVm ToVm()
    {
        return new ImportFileUploadSessionVm
        {
            SessionId = SessionId,
            UploadedBytes = UploadedBytes,
            TotalBytes = FileSize,
            Status = Status.ToString().ToLowerInvariant(),
        };
    }
}

public class ImportFileUploadChunkState
{
    public long Offset { get; set; }
    public long Length { get; set; }
    public string BlockId { get; set; } = null!;
}

public enum ImportFileUploadSessionStatus
{
    Created,
    Uploading,
    Uploaded,
    Finalizing,
    Completed,
}
