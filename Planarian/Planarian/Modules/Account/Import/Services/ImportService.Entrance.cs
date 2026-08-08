using Planarian.Library.Exceptions;
using Planarian.Modules.Account.Import.Models;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;

namespace Planarian.Modules.Account.Import.Services;

public partial class ImportService
{
    public async Task<List<EntranceDryRun>> ImportEntrancesFileProcess(string temporaryFileId, bool isDryRun,
        bool syncExisting, CancellationToken cancellationToken)
    {
        if (RequestUser.AccountId == null) throw ApiExceptionDictionary.NoAccount;
        if (string.IsNullOrWhiteSpace(temporaryFileId))
            throw ApiExceptionDictionary.NullValue(nameof(temporaryFileId));

        var signalRGroup = temporaryFileId;
        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Started planning entrance import");

        EntranceImportPlan plan;
        await using (var stream = await _fileService.GetFileStream(temporaryFileId))
        {
            var planner = new EntranceImportPlanner(_dbContext, RequestUser);
            plan = await planner.PlanAsync(stream, syncExisting, cancellationToken);
        }

        var preview = plan.CreatePreview();
        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished planning entrance import");

        // Planning is intentionally pure: a preview performs no staging writes,
        // no tag/reference creation, and does not depend on transaction rollback.
        if (isDryRun) return preview;

        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Applying entrance import");
        var snapshots = new CavePublishedSnapshotReader(_dbContext, RequestUser);
        var publisher = new ImportRevisionPublisher(_dbContext, RequestUser);
        var executor = new EntranceImportExecutor(_dbContext, RequestUser, snapshots, publisher);
        await executor.ExecuteAsync(plan, temporaryFileId, cancellationToken);

        await _notificationService.SendNotificationToGroupAsync(signalRGroup, "Finished entrance import");
        return preview;
    }
}
