using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Exceptions;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Model.Interceptors;

public class SaveChangesInterceptor : ISaveChangesInterceptor
{
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        throw new InvalidOperationException(
            "Synchronous SaveChanges is not supported because Planarian ownership validation requires asynchronous database access. Use SaveChangesAsync instead.");
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

        await SavingChangesInternalAsync(eventData, cancellationToken);
        return result;
    }

    private async Task SavingChangesInternalAsync(DbContextEventData eventData, CancellationToken cancellationToken)
    {
        if (eventData.Context == null) return;

        var context = (PlanarianDbContextBase)eventData.Context;
        var entries = eventData.Context.ChangeTracker.Entries().ToList();

        // Validate current and original Cave/County/Entrance ownership in bounded batches before
        // validating individual entities. This remains side-effect-free so authorization checks
        // cannot change EF relationship state while a SaveChanges operation is in progress.
        var ownershipValidation = await SaveChangesOwnershipBatchLoader.ValidateAsync(
            context, entries, cancellationToken);

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
                    await Validate(context, entityEntry, entityEntry.State, ownershipValidation);
                    break;
                case EntityState.Modified:
                    entity.ModifiedOn = DateTime.UtcNow;
                    entity.ModifiedByUserId = !string.IsNullOrWhiteSpace(context.RequestUser?.Id)
                        ? context.RequestUser.Id
                        : null;
                    await Validate(context, entityEntry, entityEntry.State, ownershipValidation);
                    break;
                case EntityState.Deleted:
                    await Validate(context, entityEntry, entityEntry.State, ownershipValidation);
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private async Task Validate(PlanarianDbContextBase context, EntityEntry entity, EntityState entityState,
        SaveChangesOwnershipBatchLoader.ValidationResult ownershipValidation)
    {
        var name = entity.Entity.GetType().Name;
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
                ValidateCurrentAndOriginalAccount(entity, accountState.AccountId, requestUserAccountId, nameof(AccountState.AccountId));
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
                                         !string.IsNullOrWhiteSpace(user.HashedPassword) &&
                                         !string.IsNullOrWhiteSpace(user.EmailConfirmationCode) &&
                                         user.EmailConfirmedOn == null &&
                                         string.IsNullOrWhiteSpace(user.PasswordResetCode) &&
                                         user.PasswordResetCodeExpiration == null;
                    var isInvitationCleanup = user.IsTemporary &&
                                              ownershipValidation.IsTemporaryInvitationUserTransition(user, entityState);
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
                var inSameAccount = !string.IsNullOrWhiteSpace(requestUserAccountId) &&
                                    await context.AccountUsers.AnyAsync(e =>
                                        e.UserId == user.Id && e.AccountId == requestUserAccountId);
                var canModify = (user.Id == requestUserId && !string.IsNullOrWhiteSpace(requestUserId)) ||
                                inSameAccount ||
                                (user.IsTemporary &&
                                 ownershipValidation.IsTemporaryInvitationUserTransition(user, entityState));
                if (!canModify)
                    throw RequestPolicyErrors.Forbidden("You do not have permission to modify this entity.");
                break;

            case nameof(AccountUser):
                var accountUser = (AccountUser)entity.Entity;
                var isInvitation = !string.IsNullOrWhiteSpace(accountUser.InvitationCode);
                if (entityState == EntityState.Added)
                {
                    var isSameSelectedAccount = !string.IsNullOrWhiteSpace(requestUserAccountId) &&
                                                accountUser.AccountId == requestUserAccountId;
                    var isVerifiedInvitationClaim = ownershipValidation.IsInvitationClaim(
                        accountUser.AccountId, accountUser.UserId);
                    var isPlatformAdminCreatingAccount = context.RequestUser != null &&
                                                         accountUser.UserId == context.RequestUser.Id &&
                                                         accountUser.Account != null &&
                                                         context.Entry(accountUser.Account).State == EntityState.Added &&
                                                         await context.RequestUser.HasCavePermission(
                                                             PermissionPolicyKey.PlanarianAdmin, false);
                    if (!isSameSelectedAccount && !isVerifiedInvitationClaim && !isPlatformAdminCreatingAccount)
                        throw RequestPolicyErrors.Forbidden("You do not have permission to modify this entity.");
                    break;
                }

                // Invitation acceptance/decline deletes the temporary invitation membership after validating the code.
                if (entityState == EntityState.Deleted && isInvitation)
                {
                    if (accountUser.AccountId == requestUserAccountId ||
                        string.IsNullOrWhiteSpace(context.RequestUser?.Id) ||
                        await InvitationTargetsCurrentUser(context, accountUser))
                        break;

                    throw RequestPolicyErrors.Forbidden("You do not have permission to modify this entity.");
                }
                ValidateCurrentAndOriginalAccount(entity, accountUser.AccountId, requestUserAccountId, nameof(AccountUser.AccountId));
                break;

            case nameof(TagType):
                var tagType = (TagType)entity.Entity;
                if (IsCurrentOrOriginalGlobalTagType(entity, tagType))
                {
                    await RequirePlanarianAdmin(context);
                    break;
                }
                ValidateCurrentAndOriginalAccount(entity, tagType.AccountId, requestUserAccountId, nameof(TagType.AccountId));
                break;

            case nameof(ArcheologyTag):
                ValidateParentOwnership(entity, ownershipValidation, ((ArcheologyTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(BiologyTag):
                ValidateParentOwnership(entity, ownershipValidation, ((BiologyTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(CartographerNameTag):
                ValidateParentOwnership(entity, ownershipValidation, ((CartographerNameTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(CaveOtherTag):
                ValidateParentOwnership(entity, ownershipValidation, ((CaveOtherTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(CaveReportedByNameTag):
                ValidateParentOwnership(entity, ownershipValidation, ((CaveReportedByNameTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(GeologicAgeTag):
                ValidateParentOwnership(entity, ownershipValidation, ((GeologicAgeTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(GeologyTag):
                ValidateParentOwnership(entity, ownershipValidation, ((GeologyTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(MapStatusTag):
                ValidateParentOwnership(entity, ownershipValidation, ((MapStatusTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(PhysiographicProvinceTag):
                ValidateParentOwnership(entity, ownershipValidation, ((PhysiographicProvinceTag)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;

            case nameof(Cave):
                var cave = (Cave)entity.Entity;
                ValidateCurrentAndOriginalAccount(entity, cave.AccountId, requestUserAccountId, nameof(Cave.AccountId));
                break;
            case nameof(CaveRevision):
                ValidateCurrentAndOriginalAccount(entity, ((CaveRevision)entity.Entity).AccountId, requestUserAccountId, nameof(CaveRevision.AccountId));
                break;
            case nameof(CaveImportBatch):
                ValidateCurrentAndOriginalAccount(entity, ((CaveImportBatch)entity.Entity).AccountId, requestUserAccountId, nameof(CaveImportBatch.AccountId));
                break;
            case nameof(CaveChangeRequest):
                ValidateCurrentAndOriginalAccount(entity, ((CaveChangeRequest)entity.Entity).AccountId, requestUserAccountId, nameof(CaveChangeRequest.AccountId));
                break;
            case nameof(CaveProposalVersion):
                ValidateCurrentAndOriginalAccount(entity, ((CaveProposalVersion)entity.Entity).AccountId, requestUserAccountId, nameof(CaveProposalVersion.AccountId));
                break;
            case nameof(CaveChangeRequestStagedFile):
                ValidateCurrentAndOriginalAccount(entity, ((CaveChangeRequestStagedFile)entity.Entity).AccountId, requestUserAccountId, nameof(CaveChangeRequestStagedFile.AccountId));
                break;
            case nameof(CaveChangeRequestReviewEvent):
                ValidateCurrentAndOriginalAccount(entity, ((CaveChangeRequestReviewEvent)entity.Entity).AccountId, requestUserAccountId, nameof(CaveChangeRequestReviewEvent.AccountId));
                break;
            case nameof(StorageCleanupTarget):
                ValidateCurrentAndOriginalAccount(entity, ((StorageCleanupTarget)entity.Entity).AccountId, requestUserAccountId, nameof(StorageCleanupTarget.AccountId));
                break;

            case nameof(County):
                ValidateCurrentAndOriginalAccount(entity, ((County)entity.Entity).AccountId, requestUserAccountId, nameof(County.AccountId));
                break;
            case nameof(CavePermission):
                var cavePermission = (CavePermission)entity.Entity;
                if (IsInvitationClaimPermissionTransition(
                        entity, cavePermission.AccountId, cavePermission.UserId, nameof(CavePermission.AccountId),
                        nameof(CavePermission.UserId), ownershipValidation))
                    break;
                ValidateCurrentAndOriginalAccount(entity, cavePermission.AccountId, requestUserAccountId, nameof(CavePermission.AccountId));
                break;
            case nameof(Favorite):
                var favorite = (Favorite)entity.Entity;
                ValidateCurrentAndOriginalAccount(entity, favorite.AccountId, requestUserAccountId, nameof(Favorite.AccountId));
                break;
            case nameof(UserPermission):
                var userPermission = (UserPermission)entity.Entity;
                if (IsCurrentOrOriginalGlobalAccount(entity, userPermission.AccountId, nameof(UserPermission.AccountId)))
                {
                    await RequirePlanarianAdmin(context);
                    break;
                }
                if (entityState == EntityState.Added &&
                    context.RequestUser != null &&
                    await context.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false))
                    return;
                if (IsInvitationClaimPermissionTransition(
                        entity, userPermission.AccountId, userPermission.UserId, nameof(UserPermission.AccountId),
                        nameof(UserPermission.UserId), ownershipValidation))
                    break;
                ValidateCurrentAndOriginalAccount(entity, userPermission.AccountId, requestUserAccountId, nameof(UserPermission.AccountId));
                break;

            case nameof(Entrance):
                ValidateParentOwnership(entity, ownershipValidation, ((Entrance)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(EntranceHydrologyTag):
                ValidateParentOwnership(entity, ownershipValidation, ((EntranceHydrologyTag)entity.Entity).Entrance?.Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(EntranceReportedByNameTag):
                ValidateParentOwnership(entity, ownershipValidation, ((EntranceReportedByNameTag)entity.Entity).Entrance?.Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(EntranceStatusTag):
                ValidateParentOwnership(entity, ownershipValidation, ((EntranceStatusTag)entity.Entity).Entrance?.Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(FieldIndicationTag):
                ValidateParentOwnership(entity, ownershipValidation, ((FieldIndicationTag)entity.Entity).Entrance?.Cave?.AccountId, requestUserAccountId);
                break;
            case nameof(EntranceOtherTag):
                ValidateParentOwnership(entity, ownershipValidation, ((EntranceOtherTag)entity.Entity).Entrance?.Cave?.AccountId, requestUserAccountId);
                break;

            case nameof(CaveGeoJson):
                ValidateParentOwnership(entity, ownershipValidation, ((CaveGeoJson)entity.Entity).Cave?.AccountId, requestUserAccountId);
                break;

            case nameof(FeatureSetting):
                var featureSetting = (FeatureSetting)entity.Entity;
                if (IsCurrentOrOriginalGlobalAccount(entity, featureSetting.AccountId, nameof(FeatureSetting.AccountId)))
                {
                    await RequirePlanarianAdmin(context);
                    break;
                }
                if (entityState == EntityState.Added && context.RequestUser != null &&
                    await context.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false))
                    return;
                ValidateCurrentAndOriginalAccount(entity, featureSetting.AccountId, requestUserAccountId, nameof(FeatureSetting.AccountId));
                break;

            case nameof(File):
                var file = (File)entity.Entity;
                ValidateCurrentAndOriginalAccount(entity, file.AccountId, requestUserAccountId, nameof(File.AccountId));
                break;

            case nameof(Permission):
                await RequirePlanarianAdmin(context);
                break;

            case nameof(State):
                throw RequestPolicyErrors.Forbidden("You do not have permission to modify this entity.");
        }
    }

    private static bool IsCurrentOrOriginalGlobalTagType(EntityEntry entity, TagType tagType)
    {
        if (tagType.IsDefault && string.IsNullOrWhiteSpace(tagType.AccountId)) return true;
        if (entity.State is not (EntityState.Modified or EntityState.Deleted)) return false;

        var originalAccountId = entity.Property(nameof(TagType.AccountId)).OriginalValue as string;
        var originalIsDefault = entity.Property(nameof(TagType.IsDefault)).OriginalValue is true;
        return originalIsDefault && string.IsNullOrWhiteSpace(originalAccountId);
    }

    private static bool IsCurrentOrOriginalGlobalAccount(EntityEntry entity, string? currentAccountId,
        string accountIdPropertyName)
    {
        if (string.IsNullOrWhiteSpace(currentAccountId)) return true;
        if (entity.State is not (EntityState.Modified or EntityState.Deleted)) return false;
        return string.IsNullOrWhiteSpace(entity.Property(accountIdPropertyName).OriginalValue as string);
    }

    private static async Task RequirePlanarianAdmin(PlanarianDbContextBase context)
    {
        if (context.RequestUser == null ||
            !await context.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false))
            throw RequestPolicyErrors.Forbidden("You do not have permission to modify this entity.");
    }

    private static bool IsInvitationClaimPermissionTransition(
        EntityEntry entity,
        string? accountId,
        string userId,
        string accountIdPropertyName,
        string userIdPropertyName,
        SaveChangesOwnershipBatchLoader.ValidationResult ownershipValidation)
    {
        if (entity.State != EntityState.Modified) return false;

        var businessChanges = entity.Properties
            .Where(property => property.IsModified)
            .Select(property => property.Metadata.Name)
            .Where(name => name is not nameof(EntityBase.ModifiedOn) and not nameof(EntityBase.ModifiedByUserId))
            .ToHashSet(StringComparer.Ordinal);
        if (!businessChanges.SetEquals([userIdPropertyName])) return false;

        var originalAccountId = entity.Property(accountIdPropertyName).OriginalValue as string;
        var originalUserId = entity.Property(userIdPropertyName).OriginalValue as string;
        return originalAccountId == accountId &&
               ownershipValidation.IsInvitationPermissionTransfer(accountId, userId, originalUserId);
    }

    private static async Task<bool> InvitationTargetsCurrentUser(
        PlanarianDbContextBase context,
        AccountUser invitation)
    {
        var requestUserId = context.RequestUser?.Id;
        if (string.IsNullOrWhiteSpace(requestUserId) || string.IsNullOrWhiteSpace(invitation.UserId))
            return false;

        var userIds = new[] { requestUserId, invitation.UserId };
        var emails = await context.Users.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, NormalizedEmail = user.EmailAddress.ToLower() })
            .ToListAsync();

        var requestEmail = emails.SingleOrDefault(user => user.Id == requestUserId)?.NormalizedEmail;
        var invitationEmail = emails.SingleOrDefault(user => user.Id == invitation.UserId)?.NormalizedEmail;
        return !string.IsNullOrWhiteSpace(requestEmail) && requestEmail == invitationEmail;
    }

    private static void ValidateParentOwnership(EntityEntry entity,
        SaveChangesOwnershipBatchLoader.ValidationResult ownershipValidation,
        string? navigationAccountId, string? requestUserAccountId)
    {
        if (ownershipValidation.HasValidatedParentRelationship(entity.Entity)) return;
        ValidateAccount(navigationAccountId, requestUserAccountId);
    }

    private static void ValidateCurrentAndOriginalAccount(EntityEntry entity, string? currentAccountId,
        string? requestUserAccountId, string accountIdPropertyName)
    {
        ValidateAccount(currentAccountId, requestUserAccountId);
        ValidateOriginalAccountIfPresent(entity, requestUserAccountId, accountIdPropertyName);
    }

    private static void ValidateOriginalAccountIfPresent(EntityEntry entity, string? requestUserAccountId,
        string accountIdPropertyName)
    {
        if (entity.State is not (EntityState.Modified or EntityState.Deleted)) return;

        var originalAccountId = entity.Property(accountIdPropertyName).OriginalValue as string;
        if (!string.IsNullOrWhiteSpace(originalAccountId))
            ValidateAccount(originalAccountId, requestUserAccountId);
    }

    private static void ValidateAccount(string? entityAccountId, string? requestUserAccountId)
    {
        if ((!string.IsNullOrWhiteSpace(requestUserAccountId) && requestUserAccountId != entityAccountId) ||
            (!string.IsNullOrWhiteSpace(entityAccountId) && entityAccountId != requestUserAccountId))
        {
            throw RequestPolicyErrors.Forbidden("You do not have permission to modify this entity.");
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
