using System.Collections.Concurrent;
using System.Text;
using Azure.Storage.Blobs.Specialized;
using Planarian.Library.Exceptions;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;
using Planarian.Shared.Options;

namespace Planarian.Shared.Services;

public class ChunkedUploadService : ServiceBase
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(2);
    private static readonly ConcurrentDictionary<string, ChunkedUploadSessionState> Sessions = new();
    private static readonly ConcurrentDictionary<string, byte> ActiveProcessingRequests = new();
    private static readonly object ProcessingAdmissionLock = new();
    private static SemaphoreSlim? ProcessingSemaphore;

    private readonly FileService _fileService;
    private readonly long _maxFileSizeBytes;
    private readonly long _maxChunkSizeBytes;

    public ChunkedUploadService(
        RequestUser requestUser,
        FileService fileService,
        RequestThrottleOptions requestThrottleOptions) : base(requestUser)
    {
        _fileService = fileService;
        _maxFileSizeBytes = Math.Max(1, requestThrottleOptions.ChunkedUploadMaxFileSizeBytes);
        _maxChunkSizeBytes = Math.Max(1, requestThrottleOptions.ChunkedUploadMaxChunkSizeBytes);
        InitializeProcessingAdmission(requestThrottleOptions);
    }

    public async Task<IReadOnlyCollection<ChunkedUploadSessionState>> CleanupExpiredSessions(
        CancellationToken cancellationToken)
    {
        var expired = new List<ChunkedUploadSessionState>();

        foreach (var (sessionId, session) in Sessions)
        {
            if (session.ExpiresOn > DateTimeOffset.UtcNow)
            {
                continue;
            }

            if (Sessions.TryRemove(sessionId, out var removed))
            {
                expired.Add(removed);
            }
        }

        foreach (var session in expired)
        {
            ReleaseProcessingSlot(session.SessionId);
            await _fileService.DeleteFile(session.BlobKey, session.ContainerName);
        }

        return expired;
    }

    public async Task<ChunkedUploadSessionState> CreateSession(
        ChunkedUploadSessionCreateRequest request,
        CancellationToken cancellationToken)
    {
        await CleanupExpiredSessions(cancellationToken);

        if (RequestUser.AccountId == null) throw ApiExceptionDictionary.NoAccount;
        if (string.IsNullOrWhiteSpace(RequestUser.Id)) throw ApiExceptionDictionary.NoAccount;
        if (string.IsNullOrWhiteSpace(request.FileName)) throw ApiExceptionDictionary.NullValue(nameof(request.FileName));
        if (request.FileSize <= 0) throw ApiExceptionDictionary.BadRequest("File size must be greater than 0.");
        if (request.FileSize > _maxFileSizeBytes)
            throw ApiExceptionDictionary.BadRequest(
                $"File size must be {FormatBytes(_maxFileSizeBytes)} or less.");
        if (string.IsNullOrWhiteSpace(request.BlobKeyPrefix))
            throw ApiExceptionDictionary.NullValue(nameof(request.BlobKeyPrefix));

        var sessionId = Guid.NewGuid().ToString("N");
        var session = new ChunkedUploadSessionState
        {
            SessionId = sessionId,
            AccountId = RequestUser.AccountId,
            UserId = RequestUser.Id,
            ContainerName = RequestUser.AccountContainerName,
            FileName = request.FileName,
            FileSize = request.FileSize,
            RequestId = request.RequestId,
            BlobKey = $"{request.BlobKeyPrefix.TrimEnd('/')}/{sessionId}",
            Metadata = request.Metadata,
            Status = ChunkedUploadSessionStatus.Created,
            ExpiresOn = DateTimeOffset.UtcNow.Add(SessionTtl),
        };

        Sessions[session.SessionId] = session;
        return session;
    }

    public async Task<ChunkedUploadSessionState> UploadChunk(
        string sessionId,
        Stream chunkStream,
        long offset,
        int chunkIndex,
        long contentLength,
        CancellationToken cancellationToken)
    {
        await CleanupExpiredSessions(cancellationToken);

        var session = GetRequiredSession(sessionId);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            switch (session.Status)
            {
                case ChunkedUploadSessionStatus.Canceled:
                    throw ApiExceptionDictionary.SessionCancelled();
                case ChunkedUploadSessionStatus.Completed:
                    return session;
            }

            if (contentLength <= 0)
            {
                throw ApiExceptionDictionary.BadRequest("Chunk content length must be greater than 0.");
            }

            if (contentLength > _maxChunkSizeBytes)
            {
                throw ApiExceptionDictionary.BadRequest(
                    $"Chunk size must be {FormatBytes(_maxChunkSizeBytes)} or less.");
            }

            if (offset < 0 || offset > session.FileSize)
            {
                throw ApiExceptionDictionary.BadRequest("Chunk offset is invalid.");
            }

            if (offset + contentLength > session.FileSize)
            {
                throw ApiExceptionDictionary.BadRequest("Chunk exceeds the expected file size.");
            }

            if (session.UploadedChunks.TryGetValue(chunkIndex, out var existingChunk))
            {
                if (existingChunk.Offset != offset || existingChunk.Length != contentLength)
                {
                    throw ApiExceptionDictionary.Conflict(
                        "Chunk metadata does not match the current upload session state.");
                }

                return session;
            }

            if (chunkIndex != session.NextChunkIndex || offset != session.UploadedBytes)
            {
                throw ApiExceptionDictionary.Conflict("Chunk order does not match the current upload session state.");
            }

            var containerClient = await _fileService.GetBlobContainerClient(session.ContainerName);
            var blockBlobClient = containerClient.GetBlockBlobClient(session.BlobKey);
            var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(chunkIndex.ToString("D8")));
            await using var seekableChunkStream =
                await CreateSeekableChunkStream(chunkStream, contentLength, cancellationToken);
            try
            {
                await blockBlobClient.StageBlockAsync(
                    blockId,
                    seekableChunkStream,
                    cancellationToken: cancellationToken);
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            session.UploadedChunks[chunkIndex] = new ChunkedUploadChunkState
            {
                Offset = offset,
                Length = contentLength,
                BlockId = blockId,
            };
            session.UploadedBytes += contentLength;
            session.NextChunkIndex += 1;
            session.Status = session.UploadedBytes >= session.FileSize
                ? ChunkedUploadSessionStatus.Uploaded
                : ChunkedUploadSessionStatus.Uploading;
            session.ExpiresOn = DateTimeOffset.UtcNow.Add(SessionTtl);

            return session;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task<ChunkedUploadSessionState> CommitSession(
        string sessionId,
        CancellationToken cancellationToken)
    {
        await CleanupExpiredSessions(cancellationToken);

        var session = GetRequiredSession(sessionId);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            if (session.Status == ChunkedUploadSessionStatus.Canceled)
            {
                throw ApiExceptionDictionary.SessionCancelled();
            }

            if (session.Status == ChunkedUploadSessionStatus.Completed)
            {
                return session;
            }

            if (session.UploadedBytes != session.FileSize || session.UploadedChunks.Count == 0)
            {
                throw ApiExceptionDictionary.BadRequest("The upload session is incomplete and cannot be finalized.");
            }

            await ReserveProcessingSlot(session.SessionId, cancellationToken);
            session.Status = ChunkedUploadSessionStatus.Finalizing;
            try
            {
                var containerClient = await _fileService.GetBlobContainerClient(session.ContainerName);
                var blockBlobClient = containerClient.GetBlockBlobClient(session.BlobKey);
                await blockBlobClient.CommitBlockListAsync(
                    session.UploadedChunks.OrderBy(e => e.Key).Select(e => e.Value.BlockId),
                    cancellationToken: cancellationToken);

                session.Status = ChunkedUploadSessionStatus.Completed;
                return session;
            }
            catch
            {
                Sessions.TryRemove(session.SessionId, out _);
                ReleaseProcessingSlot(session.SessionId);

                try
                {
                    await _fileService.DeleteFile(session.BlobKey, session.ContainerName);
                }
                catch
                {
                    // The commit failed and the upload attempt is already over.
                    // A cleanup failure should not hide the original commit error.
                }

                throw;
            }
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private async Task ReserveProcessingSlot(
        string requestId,
        CancellationToken cancellationToken)
    {
        var semaphore = ProcessingSemaphore ?? throw new InvalidOperationException("Upload admission is not initialized.");

        ReleaseProcessingSlotsWithoutSessions();

        if (!await semaphore.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            throw ApiExceptionDictionary.TooManyRequests(
                "Too many file uploads are currently being processed. Please retry shortly.");
        }

        ActiveProcessingRequests[requestId] = 0;
    }

    private static void ReleaseProcessingSlotsWithoutSessions()
    {
        foreach (var requestId in ActiveProcessingRequests.Keys)
        {
            if (Sessions.ContainsKey(requestId))
            {
                continue;
            }

            ReleaseProcessingSlot(requestId);
        }
    }

    public async Task CancelSession(string sessionId, CancellationToken cancellationToken)
    {
        await CleanupExpiredSessions(cancellationToken);

        var session = GetRequiredSession(sessionId);
        await CloseSession(session, cancellationToken);
    }

    public async Task CancelActiveSessionsForCurrentUser(CancellationToken cancellationToken)
    {
        await CleanupExpiredSessions(cancellationToken);

        if (RequestUser.AccountId == null) throw ApiExceptionDictionary.NoAccount;
        if (string.IsNullOrWhiteSpace(RequestUser.Id)) throw ApiExceptionDictionary.NoAccount;

        var activeSessions = Sessions.Values
            .Where(session =>
                string.Equals(session.AccountId, RequestUser.AccountId, StringComparison.Ordinal) &&
                string.Equals(session.UserId, RequestUser.Id, StringComparison.Ordinal) &&
                session.Status != ChunkedUploadSessionStatus.Completed)
            .ToList();

        foreach (var session in activeSessions)
        {
            await CloseSession(session, cancellationToken);
        }
    }

    public async Task DeleteCommittedSessionBlob(string sessionId)
    {
        var session = GetRequiredSession(sessionId);
        await _fileService.DeleteFile(session.BlobKey, session.ContainerName);
    }

    public void RemoveSession(string sessionId)
    {
        if (Sessions.TryRemove(sessionId, out var session))
        {
            session.Status = ChunkedUploadSessionStatus.Canceled;
            ReleaseProcessingSlot(sessionId);
        }
    }

    public ChunkedUploadSessionState GetRequiredSession(string sessionId)
    {
        if (RequestUser.AccountId == null) throw ApiExceptionDictionary.NoAccount;
        if (string.IsNullOrWhiteSpace(RequestUser.Id)) throw ApiExceptionDictionary.NoAccount;
        if (string.IsNullOrWhiteSpace(sessionId)) throw ApiExceptionDictionary.NullValue(nameof(sessionId));

        var session = Sessions.TryGetValue(sessionId, out var found)
            ? found
            : throw ApiExceptionDictionary.NotFound("Upload session");

        if (session.ExpiresOn <= DateTimeOffset.UtcNow)
        {
            if (Sessions.TryRemove(sessionId, out _))
            {
                ReleaseProcessingSlot(sessionId);
            }

            throw ApiExceptionDictionary.NotFound("Upload session");
        }

        if (!string.Equals(session.AccountId, RequestUser.AccountId, StringComparison.Ordinal))
        {
            throw ApiExceptionDictionary.NotFound("Upload session");
        }

        if (!string.Equals(session.UserId, RequestUser.Id, StringComparison.Ordinal))
        {
            throw ApiExceptionDictionary.NotFound("Upload session");
        }

        return session;
    }

    public TMetadata GetRequiredSessionMetadata<TMetadata>(string sessionId) where TMetadata : class
    {
        var session = GetRequiredSession(sessionId);
        return session.Metadata as TMetadata ??
               throw ApiExceptionDictionary.BadRequest("Upload session metadata is invalid.");
    }

    private static void InitializeProcessingAdmission(RequestThrottleOptions options)
    {
        if (ProcessingSemaphore != null)
        {
            return;
        }

        lock (ProcessingAdmissionLock)
        {
            if (ProcessingSemaphore != null)
            {
                return;
            }

            var maxConcurrency = Math.Max(1, options.MaxConcurrentUploads);
            ProcessingSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        }
    }

    public void ReleaseReservedProcessingSlot(string sessionId)
    {
        ReleaseProcessingSlot(sessionId);
    }

    private static void ReleaseProcessingSlot(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return;
        }

        if (!ActiveProcessingRequests.TryRemove(requestId, out _))
        {
            return;
        }

        ProcessingSemaphore?.Release();
    }

    private async Task CloseSession(ChunkedUploadSessionState session, CancellationToken cancellationToken)
    {
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            session.Status = ChunkedUploadSessionStatus.Canceled;
            Sessions.TryRemove(session.SessionId, out _);
            ReleaseProcessingSlot(session.SessionId);
            await _fileService.DeleteFile(session.BlobKey, session.ContainerName);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private static async Task<Stream> CreateSeekableChunkStream(
        Stream chunkStream,
        long contentLength,
        CancellationToken cancellationToken)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"planarian-upload-{Guid.NewGuid():N}.chunk");
        var seekableChunkStream = new FileStream(
            tempFilePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            1024 * 1024,
            System.IO.FileOptions.Asynchronous | System.IO.FileOptions.DeleteOnClose);

        await using var lengthAwareChunkStream = new LengthAwareReadStream(chunkStream, contentLength);
        try
        {
            await lengthAwareChunkStream.CopyToAsync(seekableChunkStream, cancellationToken);
            if (seekableChunkStream.Length != contentLength)
            {
                throw ApiExceptionDictionary.BadRequest("Chunk content length does not match the uploaded body length.");
            }

            seekableChunkStream.Position = 0;
            return seekableChunkStream;
        }
        catch
        {
            await seekableChunkStream.DisposeAsync();
            throw;
        }
    }

    private static string FormatBytes(long bytes)
    {
        var sizeInMb = bytes / 1024d / 1024d;
        return sizeInMb % 1 == 0
            ? $"{sizeInMb:0} MB"
            : $"{sizeInMb:0.#} MB";
    }

    private sealed class LengthAwareReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _length;
        private long _position;

        public LengthAwareReadStream(Stream inner, long length)
        {
            _inner = inner;
            _length = length;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value != _position)
                {
                    throw new NotSupportedException();
                }
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            _position += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(buffer, cancellationToken);
            _position += read;
            return read;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return ReadAndTrackPositionAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
        }

        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        private async Task<int> ReadAndTrackPositionAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var read = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
            _position += read;
            return read;
        }
    }

}

public class ChunkedUploadSessionCreateRequest
{
    public string FileName { get; set; } = null!;
    public long FileSize { get; set; }
    public string BlobKeyPrefix { get; set; } = null!;
    public string? RequestId { get; set; }
    public object? Metadata { get; set; }
}

public class ChunkedUploadSessionState
{
    public string SessionId { get; set; } = null!;
    public string AccountId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string ContainerName { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public long FileSize { get; set; }
    public string? RequestId { get; set; }
    public string BlobKey { get; set; } = null!;
    public object? Metadata { get; set; }
    public long UploadedBytes { get; set; }
    public int NextChunkIndex { get; set; }
    public ChunkedUploadSessionStatus Status { get; set; }
    public DateTimeOffset ExpiresOn { get; set; }
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public SortedDictionary<int, ChunkedUploadChunkState> UploadedChunks { get; } = new();
}

public class ChunkedUploadChunkState
{
    public long Offset { get; set; }
    public long Length { get; set; }
    public string BlockId { get; set; } = null!;
}

public enum ChunkedUploadSessionStatus
{
    Created,
    Uploading,
    Uploaded,
    Finalizing,
    Completed,
    Canceled,
}
