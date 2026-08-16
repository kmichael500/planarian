using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Model.Interceptors;

public class SaveChangesInterceptor : ISaveChangesInterceptor
{
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        return result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        return result;
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData)
    {
    }

    public async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        if (eventData.Context == null) return result;

        eventData.Context.ChangeTracker.DetectChanges();
        await SavingChangesInternalAsync(eventData, cancellationToken);
        return result;
    }

    private async Task SavingChangesInternalAsync(DbContextEventData eventData, CancellationToken cancellationToken)
    {
        if (eventData.Context == null) return;

        var context = (PlanarianDbContextBase)eventData.Context;
        var entries = eventData.Context.ChangeTracker.Entries().ToList();

        // Resolve ownership once per distinct Cave/Entrance foreign key before
        // validating individual entities. Existing validation can then use the
        // populated navigation without issuing an ownership query per row.
        await SaveChangesOwnershipBatchLoader.PopulateAsync(context, entries, cancellationToken);

        foreach (var entityEntry in entries)
        {
            if (entityEntry.Entity.GetType().BaseType != typeof(EntityBase) &&
                entityEntry.Entity.GetType().BaseType != typeof(EntityBaseNameId))
                continue;

            var entity = (EntityBase)entityEntry.Entity;
            switch (entityEntry.State)
            {
                case EntityState.Added:
                    entity.CreatedOn = DateTime.UtcNow;
                    entity.CreatedByUserId = !string.IsNullOrWhiteSpace(context.RequestUser?.Id)
                        ? context.RequestUser.Id
                        : null;
                    entity.Id = !string.IsNullOrWhiteSpace(entity.Id) && entity.Id.Length == PropertyLength.Id
                        ? entity.Id
                        : IdGenerator.Generate();
                    await Validate(context, entityEntry, entityEntry.State);
                    break;
                case EntityState.Modified:
                    entity.ModifiedOn = DateTime.UtcNow;
                    entity.ModifiedByUserId = !string.IsNullOrWhiteSpace(context.RequestUser?.Id)
                        ? context.RequestUser.Id
                        : null;
                    await Validate(context, entityEntry, entityEntry.State);
                    break;
                case EntityState.Deleted:
                    await Validate(context, entityEntry, entityEntry.State);
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private async Task Validate(PlanarianDbContextBase context, EntityEntry entity, EntityState entityState)
    {
        var name = entity.Entity.GetType().Name;
        if (name == nameof(Permission)) return;

        var requestUserAccountId = context.RequestUser?.AccountId;
        switch (name)
        {
            case nameof(Account):
                var account = (Account)entity.Entity;
                if (entityState == EntityState.Added && context.RequestUser != null &&
                    await context.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false))
                    return;
                ValidateAccount(account.Id, requestUserAccountId);
                break;

            case nameof(AccountState):
                var accountState = (AccountState)entity.Entity;
                if (entityState == EntityState.Added && context.RequestUser != null &&
                    await context.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false))
                    return;
                ValidateAccount(accountState.AccountId, requestUserAccountId);
                break;

            case nameof(User):
                var user = (User)entity.Entity;
                if (context.RequestUser == null || string.IsNullOrWhiteSpace(context.RequestUser.Id))
                {
                    var modifiedPropertyNames = entity.Properties
                        .Where(p => p.IsModified)
                        .Select(p => p.Metadata.Name)
                        .Where(p => p is not nameof(EntityBase.ModifiedOn) and not nameof(EntityBase.ModifiedByUserId))
                        .ToHashSet();

                    var isRegistration = entityState == EntityState.Added &&
                                         !user.IsTemporary &&
                                         !user.HashedPassword.IsNullOrWhiteSpace() &&
                                         !user.EmailConfirmationCode.IsNullOrWhiteSpace() &&
                                         user.EmailConfirmedOn == null &&
                                         user.PasswordResetCode.IsNullOrWhiteSpace() &&
                                         user.PasswordResetCodeExpiration == null;
                    var isInvitationCleanup = entityState == EntityState.Deleted && user.IsTemporary;
                    var isEmailConfirmation = modifiedPropertyNames.Count > 0 &&
                                              modifiedPropertyNames.All(p => p is nameof(User.EmailAddress)
                                                  or nameof(User.PendingEmailAddress)
                                                  or nameof(User.SessionVersion)
                                                  or nameof(User.EmailConfirmedOn)
                                                  or nameof(User.EmailConfirmationCode)
                                                  or nameof(User.EmailConfirmationMessageLogId));
                    var isPasswordResetEmail = modifiedPropertyNames.Count > 0 &&
                                               modifiedPropertyNames.All(p => p is nameof(User.PasswordResetCode)
                                                   or nameof(User.PasswordResetCodeExpiration));
                    var isPasswordResetCompletion = modifiedPropertyNames.Count > 0 &&
                                                    modifiedPropertyNames.All(p => p is nameof(User.HashedPassword)
                                                        or nameof(User.SessionVersion)
                                                        or nameof(User.PasswordResetCode)
                                                        or nameof(User.PasswordResetCodeExpiration)) &&
                                                    string.IsNullOrWhiteSpace(user.PasswordResetCode) &&
                                                    user.PasswordResetCodeExpiration == null;

                    if (isRegistration || isInvitationCleanup || isEmailConfirmation || isPasswordResetEmail ||
                        isPasswordResetCompletion)
                        return;

                    throw new NullReferenceException("RequestUser or its Id is null.");
                }

                var requestUserId = context.RequestUser.Id;
                var inSameAccount = await context.AccountUsers.AnyAsync(e =>
                    e.UserId == user.Id && e.AccountId == requestUserAccountId);
                var isResetPassword = string.IsNullOrWhiteSpace(requestUserAccountId);
                var canModify = isResetPassword ||
                                (user.Id == requestUserId && !string.IsNullOrWhiteSpace(requestUserId)) ||
                                inSameAccount || user.IsTemporary;
                if (!canModify)
                    throw ApiExceptionDictionary.Forbidden("You do not have permission to modify this entity.");
                break;

            case nameof(AccountUser):
                var accountUser = (AccountUser)entity.Entity;
                var isInvitation = !accountUser.InvitationCode.IsNullOrWhiteSpace();
                if (!isInvitation && entityState != EntityState.Added)
                    ValidateAccount(accountUser.AccountId, requestUserAccountId);
                break;

            case nameof(TagType):
                var tagType = (TagType)entity.Entity;
                if (!tagType.IsDefault || !string.IsNullOrWhiteSpace(tagType.AccountId))
                    ValidateAccount(tagType.AccountId, requestUserAccountId);
                break;

            case nameof(ArcheologyTag):
                ValidateAccount(((ArcheologyTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(BiologyTag):
                ValidateAccount(((BiologyTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(CartographerNameTag):
                ValidateAccount(((CartographerNameTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(CaveOtherTag):
                ValidateAccount(((CaveOtherTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(CaveReportedByNameTag):
                ValidateAccount(((CaveReportedByNameTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(GeologicAgeTag):
                ValidateAccount(((GeologicAgeTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(GeologyTag):
                ValidateAccount(((GeologyTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(MapStatusTag):
                ValidateAccount(((MapStatusTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(PhysiographicProvinceTag):
                ValidateAccount(((PhysiographicProvinceTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;

            case nameof(Cave):
                ValidateAccount(((Cave)entity.Entity).AccountId, requestUserAccountId);
                break;
            case nameof(CaveRevision):
                ValidateAccount(((CaveRevision)entity.Entity).AccountId, requestUserAccountId);
                break;
            case nameof(CaveImportBatch):
                ValidateAccount(((CaveImportBatch)entity.Entity).AccountId, requestUserAccountId);
                break;
            case nameof(CaveChangeRequest):
                ValidateAccount(((CaveChangeRequest)entity.Entity).AccountId, requestUserAccountId);
                break;
            case nameof(CaveProposalVersion):
                ValidateAccount(((CaveProposalVersion)entity.Entity).AccountId, requestUserAccountId);
                break;
            case nameof(CaveChangeRequestStagedFile):
                ValidateAccount(((CaveChangeRequestStagedFile)entity.Entity).AccountId, requestUserAccountId);
                break;

            case nameof(County):
                ValidateAccount(((County)entity.Entity).AccountId, requestUserAccountId);
                break;

            case nameof(Entrance):
                ValidateAccount(((Entrance)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(EntranceHydrologyTag):
                ValidateAccount(((EntranceHydrologyTag)entity.Entity).Entrance?.Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(EntranceReportedByNameTag):
                ValidateAccount(((EntranceReportedByNameTag)entity.Entity).Entrance?.Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(EntranceStatusTag):
                ValidateAccount(((EntranceStatusTag)entity.Entity).Entrance?.Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(FieldIndicationTag):
                ValidateAccount(((FieldIndicationTag)entity.Entity).Entrance?.Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(EntranceOtherTag):
                ValidateAccount(((EntranceOtherTag)entity.Entity).Entrance?.Cave?.AccountId, requestUserAccountId);
                break;

            case nameof(FeatureSetting):
                var featureSetting = (FeatureSetting)entity.Entity;
                if (entityState == EntityState.Added && context.RequestUser != null &&
                    await context.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false))
                    return;
                ValidateAccount(featureSetting.AccountId, requestUserAccountId);
                break;

            case nameof(File):
                var file = (File)entity.Entity;
                ValidateAccount(file.Cave?.AccountId ?? file.AccountId, requestUserAccountId);
                break;

            case nameof(State):
                throw ApiExceptionDictionary.Forbidden("You do not have permission to modify this entity.");
        }
    }

    private static void ValidateAccount(string? entityAccountId, string? requestUserAccountId)
    {
        if ((!string.IsNullOrWhiteSpace(requestUserAccountId) && requestUserAccountId != entityAccountId) ||
            (!string.IsNullOrWhiteSpace(entityAccountId) && entityAccountId != requestUserAccountId))
        {
            throw ApiExceptionDictionary.Forbidden("You do not have permission to modify this entity.");
        }
    }

    public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = new())
    {
        return new ValueTask<int>(result);
    }

    public Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = new())
    {
        return Task.CompletedTask;
    }
}
