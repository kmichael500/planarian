using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.PlanarianSettings.Models;
using Planarian.Modules.PlanarianSettings.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.PlanarianSettings.Services;

public class PlanarianSettingsService : ServiceBase<PlanarianSettingsRepository>
{
    public PlanarianSettingsService(PlanarianSettingsRepository repository, RequestUser requestUser) : base(repository,
        requestUser)
    {
    }

    public async Task<string> CreateAccount(CreateAccountVm account, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.Name))
        {
            throw ApiExceptionDictionary.BadRequest("Name cannot be empty.");
        }

        var stateIds = account.StateIds
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .ToList();

        if (stateIds.Count == 0)
        {
            throw ApiExceptionDictionary.BadRequest("At least one state is required.");
        }

        var existingStateIds = await Repository.GetExistingStateIds(stateIds, cancellationToken);
        var missingStateIds = stateIds.Except(existingStateIds).ToList();
        if (missingStateIds.Count > 0)
        {
            throw ApiExceptionDictionary.BadRequest("One or more selected states are invalid.");
        }

        var adminPermission = await Repository.GetPermissionByKey(PermissionKey.Admin, cancellationToken);
        if (adminPermission == null)
        {
            throw ApiExceptionDictionary.NotFound("Admin permission");
        }

        var entity = new global::Planarian.Model.Database.Entities.RidgeWalker.Account
        {
            Name = account.Name.Trim(),
            CountyIdDelimiter = account.CountyIdDelimiter?.Trim(),
            DefaultViewAccessAllCaves = account.DefaultViewAccessAllCaves,
            ExportEnabled = account.ExportEnabled
        };

        foreach (var key in Enum.GetValues<FeatureKey>())
        {
            var isEnabled = key switch
            {
                FeatureKey.EnabledFieldCaveGeologicAgeTags => false,
                _ => true
            };

            entity.FeatureSettings.Add(new FeatureSetting
            {
                AccountId = entity.Id,
                Key = key,
                IsEnabled = isEnabled
            });
        }

        entity.AccountUsers.Add(new AccountUser
        {
            UserId = RequestUser.Id,
        });

        foreach (var stateId in stateIds)
        {
            entity.AccountStates.Add(new AccountState
            {
                AccountId = entity.Id,
                StateId = stateId
            });
        }

        entity.UserPermissions.Add(new UserPermission
        {
            AccountId = entity.Id,
            UserId = RequestUser.Id,
            PermissionId = adminPermission.Id
        });

        Repository.Add(entity);
        await Repository.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
