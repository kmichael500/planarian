using Planarian.Library.Constants;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Notifications.Services;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Exceptions;

namespace Planarian.Modules.Account.Services;

public class AccountService : ServiceBase<AccountRepository>
{
    private readonly FileService _fileService;
    private readonly FileRepository _fileRepository;
    private readonly NotificationService _notificationService;
    private readonly TagRepository _tagRepository;

    public AccountService(AccountRepository repository, RequestUser requestUser, FileService fileService,
        FileRepository fileRepository, NotificationService notificationService, TagRepository tagRepository) : base(
        repository, requestUser)
    {
        _fileService = fileService;
        _fileRepository = fileRepository;
        _notificationService = notificationService;
        _tagRepository = tagRepository;
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

    public async Task<IEnumerable<TagTypeTableVm>> GetTagsForTable(string tagTypeKey,
        CancellationToken cancellationToken)
    {
        return await Repository.GetTagsForTable(tagTypeKey, cancellationToken);
    }

    public async Task<TagTypeTableVm> CreateOrUpdateTagType(CreateEditTagTypeVm tag, string tagTypeId)
    {
        if (string.IsNullOrWhiteSpace(tag.Name))
        {
            throw ApiExceptionDictionary.BadRequest("Name cannot be empty.");
        }
        
        var isNewTagType = string.IsNullOrWhiteSpace(tagTypeId);
        var entity = !isNewTagType ? await _tagRepository.GetTag(tagTypeId) : new TagType();

        if (entity == null)
        {
            throw ApiExceptionDictionary.NotFound("Tag Type Id");
        }

        if (entity.IsDefault)
        {
            throw ApiExceptionDictionary.Unauthorized("Cannot modify default tag types.");
        }
        

        entity.Name = tag.Name;

        if (isNewTagType)
        {
            if (!TagTypeKeyConstant.IsValidAccountTagKey(tag.Key))
            {
                throw ApiExceptionDictionary.BadRequest("Invalid tag key.");
            }

            entity.Key = tag.Key;
            entity.AccountId = RequestUser.AccountId;
            _tagRepository.Add(entity);
        }

        await _tagRepository.SaveChangesAsync();

        var result = new TagTypeTableVm
        {
            TagTypeId = entity.Id,
            Name = entity.Name,
            IsUserModifiable = !string.IsNullOrWhiteSpace(entity.AccountId),
            Occurrences = await Repository.GetNumberOfOccurrences(entity.Id)
        };
        
        return result;
    }

    public async Task<int> DeleteTagTypes(IEnumerable<string> tagTypeIds)
    {
        var result = await Repository.DeleteTagsAsync(tagTypeIds, CancellationToken.None);

        return result;
    }

    public async Task MergeTagTypes(string[] tagTypeIds, string destinationTagTypeId)
    {
        foreach (var id in tagTypeIds)
        {
            var tagType = await _tagRepository.GetTag(id);
            await Repository.MergeTagTypes(tagTypeIds, destinationTagTypeId);
            
        }
    }
}