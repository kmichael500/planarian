from pathlib import Path


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)

path = Path("Planarian/Planarian/Modules/Files/Services/FileService.cs")
text = path.read_text()

old = '''        stream.Position = 0;
        await AddToBlobStorage(stream, blobKey, RequestUser.AccountContainerName, cancellationToken);

        entity.BlobKey = blobKey;
        entity.BlobContainer = RequestUser.AccountContainerName;
        await Repository.SaveChangesAsync(cancellationToken);
        await _caveMutationCoordinator.PublishPreparedAsync(
            revisionPreparation, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var fileInformation = new FileVm'''
new = '''        try
        {
            stream.Position = 0;
            await AddToBlobStorage(stream, blobKey, RequestUser.AccountContainerName, cancellationToken);

            entity.BlobKey = blobKey;
            entity.BlobContainer = RequestUser.AccountContainerName;
            await Repository.SaveChangesAsync(cancellationToken);
            await _caveMutationCoordinator.PublishPreparedAsync(
                revisionPreparation, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // The blob store is outside the PostgreSQL transaction. The key is
            // unique to this newly-created File, so deleting it is safe even if
            // upload failed before the blob became visible.
            await BestEffortDeleteBlobAsync(blobKey, RequestUser.AccountContainerName);
            throw;
        }

        var fileInformation = new FileVm'''
text = replace_once(text, old, new, "Cave upload compensation")

old = '''        var copyOperation = await finalBlobClient.StartCopyFromUriAsync(
            sourceBlobClient.Uri,
            cancellationToken: cancellationToken);
        await copyOperation.WaitForCompletionAsync(cancellationToken);

        entity.BlobKey = blobKey;
        entity.BlobContainer = RequestUser.AccountContainerName;
        await Repository.SaveChangesAsync(cancellationToken);
        await _caveMutationCoordinator.PublishPreparedAsync(
            revisionPreparation, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var fileInformation = new FileVm'''
new = '''        try
        {
            var copyOperation = await finalBlobClient.StartCopyFromUriAsync(
                sourceBlobClient.Uri,
                cancellationToken: cancellationToken);
            await copyOperation.WaitForCompletionAsync(cancellationToken);

            entity.BlobKey = blobKey;
            entity.BlobContainer = RequestUser.AccountContainerName;
            await Repository.SaveChangesAsync(cancellationToken);
            await _caveMutationCoordinator.PublishPreparedAsync(
                revisionPreparation, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // Never remove the staged source on failure. Only compensate the
            // deterministic destination created for this publication attempt.
            await BestEffortDeleteBlobAsync(blobKey, RequestUser.AccountContainerName);
            throw;
        }

        var fileInformation = new FileVm'''
text = replace_once(text, old, new, "staged publication compensation")

old = '''        await RemoveExpiredFiles();

        var tempCaveImportTagType ='''
new = '''        var expiredBlobs = await RemoveExpiredFiles(cancellationToken);

        var tempCaveImportTagType ='''
text = replace_once(text, old, new, "defer expired blob cleanup")

old = '''        await AddToBlobStorage(stream, blobKey, RequestUser.AccountContainerName, cancellationToken);

        entity.BlobKey = blobKey;
        entity.BlobContainer = RequestUser.AccountContainerName;


        Repository.Add(entity);
        await Repository.SaveChangesAsync();
        await transaction.CommitAsync(cancellationToken);

        var fileInformation = new FileVm'''
new = '''        try
        {
            await AddToBlobStorage(stream, blobKey, RequestUser.AccountContainerName, cancellationToken);

            entity.BlobKey = blobKey;
            entity.BlobContainer = RequestUser.AccountContainerName;

            Repository.Add(entity);
            await Repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await BestEffortDeleteBlobAsync(blobKey, RequestUser.AccountContainerName);
            throw;
        }

        foreach (var expiredBlob in expiredBlobs)
            await BestEffortDeleteBlobAsync(expiredBlob.BlobKey, expiredBlob.BlobContainer);

        var fileInformation = new FileVm'''
text = replace_once(text, old, new, "temporary upload compensation")

old = '''    private async Task RemoveExpiredFiles()
    {
        var expiredFiles = await Repository.GetExpiredFiles();
        foreach (var expiredFile in expiredFiles)
        {
            await DeleteFile(expiredFile.BlobKey, expiredFile.BlobContainer);
            Repository.Delete(expiredFile);
        }

        await Repository.SaveChangesAsync();
    }

    #region Blob Storage'''
new = '''    private async Task<List<BlobDeleteTarget>> RemoveExpiredFiles(CancellationToken cancellationToken)
    {
        var expiredFiles = await Repository.GetExpiredFiles();
        var blobs = expiredFiles
            .Where(file => !string.IsNullOrWhiteSpace(file.BlobKey) && !string.IsNullOrWhiteSpace(file.BlobContainer))
            .Select(file => new BlobDeleteTarget(file.BlobKey!, file.BlobContainer!))
            .ToList();

        foreach (var expiredFile in expiredFiles)
            Repository.Delete(expiredFile);

        await Repository.SaveChangesAsync(cancellationToken);
        return blobs;
    }

    private async Task BestEffortDeleteBlobAsync(string blobKey, string blobContainer)
    {
        try
        {
            var client = await GetBlobContainerClient(blobContainer, createIfNotExists: false);
            await client.GetBlobClient(blobKey).DeleteIfExistsAsync(cancellationToken: CancellationToken.None);
        }
        catch
        {
            // Compensation must never replace the original database/upload
            // exception. An out-of-band cleanup process can retry a rare blob
            // deletion failure without corrupting relational state.
        }
    }

    private sealed record BlobDeleteTarget(string BlobKey, string BlobContainer);

    #region Blob Storage'''
text = replace_once(text, old, new, "expired blob cleanup implementation")

path.write_text(text)
