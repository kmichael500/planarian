using Planarian.Library.Options;
using Planarian.Model.Shared;
using Planarian.Modules.App.Models;
using Planarian.Modules.App.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.App.Services;

public class AppService : ServiceBase<AppRepository>
{
    private readonly ServerOptions _serverOptions;
    private readonly UserRepository _userRepository;

    public AppService(AppRepository repository, RequestUser requestUser, ServerOptions serverOptions, UserRepository userRepository) : base(repository,
        requestUser)
    {
        _serverOptions = serverOptions;
        _userRepository = userRepository;
    }

    public async Task<AppInitializeVm> Initialize(string serverBaseUrl)
    {
#if DEBUG
        _serverOptions.ServerBaseUrl = serverBaseUrl;
#endif
        var result = new AppInitializeVm(
            _serverOptions.ServerBaseUrl, $"{_serverOptions.ServerBaseUrl}/api/notificationHub");

        if (string.IsNullOrWhiteSpace(RequestUser.Id)) return result; // if not authenticated

        var accountIds = await Repository.GetAccountIds();

        var defaultAccountId = accountIds.FirstOrDefault()?.Value;
        var currentAccountId = RequestUser.AccountId ?? defaultAccountId;
        var permissions = string.IsNullOrWhiteSpace(currentAccountId)
            ? Array.Empty<string>()
            : (await _userRepository.GetPermissions(RequestUser.Id, currentAccountId)).ToArray();

        result.CurrentUser = new AppInitializeCurrentUserVm(
            RequestUser.Id,
            RequestUser.FullName,
            currentAccountId);
        result.AccountIds = accountIds;
        result.Permissions = permissions;
        return result;
    }

    public async Task<bool> HasCavePermission(string permissionKey, string? caveId, string? countyId)
    {
        return await RequestUser.HasCavePermission(permissionKey, caveId, countyId, false);
    }
}
