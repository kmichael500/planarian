using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Notifications.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Services;

public class AccountService : ServiceBase<AccountRepository>
{
    private readonly FileService _fileService;
    private readonly FileRepository _fileRepository;
    private readonly NotificationService _notificationService;

    public AccountService(AccountRepository repository, RequestUser requestUser, FileService fileService,
        FileRepository fileRepository, NotificationService notificationService) : base(
        repository, requestUser)
    {
        _fileService = fileService;
        _fileRepository = fileRepository;
        _notificationService = notificationService;
    }

    public async Task ResetAccount(CancellationToken cancellationToken)
    {
        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var deleteAllCavesSignalRGroupName = $"{RequestUser.UserGroupPrefix}-DeleteAllCaves";
            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                "Getting associated files.");
            var blobProperties = (await _fileRepository.GetAllCavesBlobProperties()).ToList();


            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                "Done getting associated files.");
            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                $"Deleted 0 of 0 caves.");

            async void DeleteCavesProgressHandler(string message)
            {
                await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName, message);
            }

            await Repository.DeleteAlLCaves(new Progress<string>(DeleteCavesProgressHandler), cancellationToken);

            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                "Deleting associated files.");
            var totalDeleted = 0;
            var totalFiles = blobProperties.Count();
            int notifyInterval =
                (blobProperties.Count > 0) ? (int)(totalFiles * 0.1) : 0; // every 10% if totalRecords is greater than 0
            notifyInterval = Math.Max(notifyInterval, 1);
            foreach (var blobProperty in blobProperties)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _fileService.DeleteFile(blobProperty.BlobKey, blobProperty.ContainerName);

                totalDeleted++;

                if (totalDeleted % notifyInterval == 0 || totalDeleted == 1 || totalDeleted == totalFiles)
                {
                    var message =
                        $"Deleted {totalDeleted} out of {totalFiles} records.";
                    await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName, message);
                }
            }
            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                "Done deleting associated files.");

     

            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                "Deleting associated counties");
            await Repository.DeleteAllCounties();
            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                "Finished deleting associated counties");

            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                "Deleting associated states");
            await Repository.DeleteAllAccountStates();
            await _notificationService.SendNotificationToGroupAsync(deleteAllCavesSignalRGroupName,
                "Finished deleting associated states");

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}