using Planarian.Library.Exceptions;
using Planarian.Modules.Account.Import.Models;
using Planarian.Shared.Services;

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

        Planarian.Modules.Import.Planning.CaveImportPlan plan;
        await using (var stream = await _fileService.GetFileStream(temporaryFileId))
        {
            var records = await _caveParser.ParseAsync(stream, cancellationToken);
            var state = await _cavePlanningRepository.LoadAsync(records, syncExisting, cancellationToken);
            plan = _cavePlanner.Plan(records, state, syncExisting, cancellationToken);
        }

        var preview = plan.CreatePreview(omitNoChange: isDryRun);
        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished planning cave import");

        // Dry run ends here. Planning is read-only, so there is no transaction to
        // roll back and no reference/tag rows are created merely to build preview.
        if (isDryRun) return preview;

        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Applying cave import");
        var result = await _caveExecutor.ExecuteAsync(plan, temporaryFileId, cancellationToken);

        // Relational state and revision history commit atomically inside the
        // executor. Blob deletion is intentionally deferred until after commit.
        foreach (var blob in result.BlobDeletes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(blob.BlobKey) && !string.IsNullOrWhiteSpace(blob.BlobContainer))
                await _fileService.DeleteObjectBestEffortAsync(
                    new StorageObjectAddress(blob.BlobContainer, blob.BlobKey));
        }

        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished cave import");
        return preview;
    }
}
