using System.Linq.Expressions;
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

public class  SaveChangesInterceptor : ISaveChangesInterceptor
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

        await SavingChangesInternalAsync(eventData);

        return result;
    }

    private async Task SavingChangesInternalAsync(DbContextEventData eventData)
    {
        if (eventData.Context == null) return;
        
        var context = (PlanarianDbContext)eventData.Context;
        foreach (var entityEntry in eventData.Context.ChangeTracker.Entries())
            if (entityEntry.Entity.GetType().BaseType == typeof(EntityBase) ||
                entityEntry.Entity.GetType().BaseType == typeof(EntityBaseNameId))
            {
                var entity = (EntityBase) entityEntry.Entity;

                switch (entityEntry.State)
                {
                    case EntityState.Added:
                        entity.CreatedOn = DateTime.UtcNow;
                        entity.CreatedByUserId =
                            !string.IsNullOrWhiteSpace(context?.RequestUser?.Id)
                                ? context.RequestUser.Id
                                : null;
                        entity.Id = !string.IsNullOrWhiteSpace(((EntityBase)entityEntry.Entity).Id) &&
                                    ((EntityBase)entityEntry.Entity).Id.Length == PropertyLength.Id
                            ? entity.Id // use the provided id if it matches our id format
                            : IdGenerator.Generate();
                        await Validate(context!, entityEntry, entityEntry.State);
                        break;
                    case EntityState.Modified:
                        entity.ModifiedOn = DateTime.UtcNow;
                        entity.ModifiedByUserId =
                            !string.IsNullOrWhiteSpace(context?.RequestUser?.Id)
                                ? context.RequestUser.Id
                                : null;
                        
                        await Validate(context!, entityEntry, entityEntry.State);
                        break;
                    case EntityState.Detached:
                        break;
                    case EntityState.Unchanged:
                        break;
                    case EntityState.Deleted:
                        await Validate(context!, entityEntry, entityEntry.State);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
    }

    private async Task Validate(PlanarianDbContext context, EntityEntry entity, EntityState entityState)
    {
        var name = entity.Entity.GetType().Name;
        var requestUserAccountId = context.RequestUser.AccountId;
        switch (name)
        {
            case nameof(Account):
                var account = (Account)entity.Entity;
                ValidateAccount(account.Id, requestUserAccountId);
                break;
            case nameof(AccountState):
                var accountState = (AccountState)entity.Entity;
                ValidateAccount(accountState.AccountId, requestUserAccountId);
                break;
            case nameof(User):
                var user = (User)entity.Entity;
                var requestUserId = context.RequestUser.Id;

                var inSameAccount = await context.AccountUsers.AnyAsync(e =>
                    e.UserId == user.Id && e.AccountId == requestUserAccountId);
            
                var isResetPassword = string.IsNullOrWhiteSpace(requestUserAccountId); // should probably be more thorough in the future
                var canModify = isResetPassword ||
                                (user.Id == requestUserId && !string.IsNullOrWhiteSpace(requestUserId)) ||
                                inSameAccount || user.IsTemporary;

                if (!canModify)
                {
                    throw ApiExceptionDictionary.Unauthorized(
                        "You do not have permission to modify this entity.");
                }

                break;
            case nameof(AccountUser):
                 var accountUser = (AccountUser)entity.Entity;
                 var isInvitation = !accountUser.InvitationCode.IsNullOrWhiteSpace();
                 if (!isInvitation && entityState != EntityState.Added)
                 {
                     ValidateAccount(accountUser.AccountId, requestUserAccountId);
                 }

                 break;
            case nameof(TagType):
                var tagType = (TagType)entity.Entity;
                ValidateAccount(tagType.AccountId, requestUserAccountId);
                break;
            case nameof(ArcheologyTag):
                var archeologyTag = (ArcheologyTag)entity.Entity;

                var archeologyTagAccountId = archeologyTag.Cave?.AccountId ?? await context.ArcheologyTags
                    .Where(e => e.Id == archeologyTag.Id)
                    .Select(e => e.Cave!.AccountId)
                    .FirstOrDefaultAsync();

                ValidateAccount(archeologyTagAccountId, requestUserAccountId);
                break;
            case nameof(BiologyTag):
                var biologyTag = (BiologyTag)entity.Entity;
                var biologyTagAccountId = biologyTag.Cave?.AccountId ?? await context.BiologyTags
                    .Where(e => e.Id == biologyTag.Id)
                    .Select(e => e.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(biologyTagAccountId, requestUserAccountId);
                break;
            case nameof(CartographerNameTag):
                var cartographerNameTag = (CartographerNameTag)entity.Entity;
                var cartographerNameTagAccountId = cartographerNameTag.Cave?.AccountId ?? await context
                    .CartographerNameTags
                    .Where(e => e.Id == cartographerNameTag.Id)
                    .Select(e => e.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(cartographerNameTagAccountId, requestUserAccountId);
                break;
            case nameof(Cave):
                var cave = (Cave)entity.Entity;
                ValidateAccount(cave.AccountId, requestUserAccountId);
                break;
            case nameof(CaveOtherTag):
                var caveOtherTag = (CaveOtherTag)entity.Entity;
                var caveOtherTagAccountId = caveOtherTag.Cave?.AccountId ?? await context.CaveOtherTags
                    .Where(e => e.Id == caveOtherTag.Id)
                    .Select(e => e.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(caveOtherTagAccountId, requestUserAccountId);
                break;
            case nameof(CaveReportedByNameTag):
                var caveReportedByNameTag = (CaveReportedByNameTag)entity.Entity;
                var caveReportedByNameTagAccountId = caveReportedByNameTag.Cave?.AccountId ?? await context
                    .CaveReportedByNameTags
                    .Where(e => e.Id == caveReportedByNameTag.Id)
                    .Select(e => e.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(caveReportedByNameTagAccountId, requestUserAccountId);
                break;
            case nameof(County):
                var county = (County)entity.Entity;
                ValidateAccount(county.AccountId, requestUserAccountId);
                break;
            case nameof(Entrance):
                var entrance = (Entrance)entity.Entity;
                var entranceAccountId = entrance.Cave?.AccountId ?? await context.Entrances
                    .Where(e => e.Id == entrance.Id)
                    .Select(e => e.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(entranceAccountId, requestUserAccountId);
                break;
            case nameof(EntranceHydrologyTag):
                var entranceHydrologyTag = (EntranceHydrologyTag)entity.Entity;
                var entranceHydrologyTagAccountId = entranceHydrologyTag.Entrance?.Cave?.AccountId ?? await context
                    .EntranceHydrologyTags
                    .Where(e => e.Id == entranceHydrologyTag.Id)
                    .Select(e => e.Entrance!.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(entranceHydrologyTagAccountId, requestUserAccountId);
                break;
            case nameof(EntranceReportedByNameTag):
                var entranceReportedByNameTag = (EntranceReportedByNameTag)entity.Entity;
                var entranceReportedByNameTagAccountId = entranceReportedByNameTag.Entrance?.Cave?.AccountId ?? await context.EntranceReportedByNameTags
                    .Where(e => e.Id == entranceReportedByNameTag.Id)
                    .Select(e => e.Entrance!.Cave!.AccountId)
                    .FirstOrDefaultAsync();

                ValidateAccount(entranceReportedByNameTagAccountId, requestUserAccountId);
                break;
            case nameof(EntranceStatusTag):
                var entranceStatusTag = (EntranceStatusTag)entity.Entity;
                var entranceStatusTagAccountId = entranceStatusTag.Entrance?.Cave?.AccountId ?? await context
                    .EntranceStatusTags
                    .Where(e => e.Id == entranceStatusTag.Id)
                    .Select(e => e.Entrance!.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(entranceStatusTagAccountId, requestUserAccountId);
                break;
            case nameof(FeatureSetting):
                var featureSetting = (FeatureSetting)entity.Entity;
                ValidateAccount(featureSetting.AccountId, requestUserAccountId);
                break;
            case nameof(FieldIndicationTag):
                var fieldIndicationTag = (FieldIndicationTag)entity.Entity;
                var fieldIndicationTagAccountId = fieldIndicationTag.Entrance?.Cave?.AccountId ?? await context
                    .FieldIndicationTags
                    .Where(e => e.Id == fieldIndicationTag.Id)
                    .Select(e => e.Entrance!.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(fieldIndicationTagAccountId, requestUserAccountId);
                break;
            case nameof(File):
                var file = (File)entity.Entity;
                var fileAccountId = file.Cave?.AccountId ?? file.AccountId ?? await context.Files
                    .Where(e => e.Id == file.Id)
                    .Select(e => e.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(fileAccountId, requestUserAccountId);
                break;
            case nameof(GeologicAgeTag):
                var geologicAgeTag = (GeologicAgeTag)entity.Entity;
                var geologicAgeTagAccountId = geologicAgeTag.Cave?.AccountId ?? await context.GeologicAgeTags
                    .Where(e => e.Id == geologicAgeTag.Id)
                    .Select(e => e.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(geologicAgeTagAccountId, requestUserAccountId);
                break;
            case nameof(MapStatusTag):
                var mapStatusTag = (MapStatusTag)entity.Entity;
                var mapStatusTagAccountId = mapStatusTag.Cave?.AccountId ?? await context.MapStatusTags
                    .Where(e => e.Id == mapStatusTag.Id)
                    .Select(e => e.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(mapStatusTagAccountId, requestUserAccountId);
                break;
            case nameof(PhysiographicProvinceTag):
                var physiographicProvinceTag = (PhysiographicProvinceTag)entity.Entity;
                var physiographicProvinceTagAccountId = physiographicProvinceTag.Cave?.AccountId ?? await context
                    .PhysiographicProvinceTags
                    .Where(e => e.Id == physiographicProvinceTag.Id)
                    .Select(e => e.Cave!.AccountId)
                    .FirstOrDefaultAsync();
                ValidateAccount(physiographicProvinceTagAccountId, requestUserAccountId);
                break;
            case nameof(State):
                throw ApiExceptionDictionary.Unauthorized("You do not have permission to modify this entity.");

        }
    }

    private void ValidateAccount(string? entityAccountId, string? requestUserAccountId)
    {

        if ((!string.IsNullOrWhiteSpace(requestUserAccountId) && requestUserAccountId != entityAccountId) ||
            !string.IsNullOrWhiteSpace(entityAccountId) && entityAccountId != requestUserAccountId)
        {
            throw ApiExceptionDictionary.Unauthorized("You do not have permission to modify this entity.");
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

