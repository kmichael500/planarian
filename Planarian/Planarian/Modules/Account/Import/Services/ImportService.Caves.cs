using Planarian.Library.Exceptions;
using Planarian.Modules.Account.Import.Models;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;

namespace Planarian.Modules.Account.Import.Services;

public partial class ImportService
{
    public async Task<List<CaveDryRunRecord>> ImportCavesFileProcess(string temporaryFileId, bool isDryRun,
        bool syncExisting, CancellationToken cancellationToken)
    {
        if (RequestUser.AccountId == null) throw ApiExceptionDictionary.NoAccount;
        if (string.IsNullOrWhiteSpace(temporaryFileId))
            throw ApiExceptionDictionary.NullValue(nameof(temporaryFileId));

        var signalRGroup = temporaryFileId;
        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Started planning cave import");

        CaveImportPlan plan;
        await using (var stream = await _fileService.GetFileStream(temporaryFileId))
        {
            var planner = new CaveImportPlanner(_dbContext, RequestUser);
            plan = await planner.PlanAsync(stream, syncExisting, cancellationToken);
        }

        var preview = plan.CreatePreview(omitNoChange: isDryRun);
        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished planning cave import");

        // Dry run ends here. Planning is read-only, so there is no transaction to
        // roll back and no reference/tag rows are created merely to build preview.
        if (isDryRun) return preview;

        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Applying cave import");
        var snapshots = new CavePublishedSnapshotReader(_dbContext, RequestUser);
        var publisher = new ImportRevisionPublisher(_dbContext, RequestUser);
        var executor = new CaveImportExecutor(_dbContext, RequestUser, snapshots, publisher);
        var result = await executor.ExecuteAsync(plan, temporaryFileId, cancellationToken);

        // Relational state and revision history commit atomically inside the
        // executor. Blob deletion is intentionally deferred until after commit.
        foreach (var blob in result.BlobDeletes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(blob.BlobKey) && !string.IsNullOrWhiteSpace(blob.BlobContainer))
                await _fileService.DeleteFile(blob.BlobKey, blob.BlobContainer);
        }

        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished cave import");
        return preview;
    }
}
