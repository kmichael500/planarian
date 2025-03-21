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
        
        if(string.IsNullOrWhiteSpace(RequestUser.AccountId)) return result;
        
        var permissions = await _userRepository.GetPermissions(RequestUser.Id, RequestUser.AccountId);
        
        result.AccountIds = accountIds;
        result.Permissions = permissions;
        return result;
    }

    public async Task<bool> HasCavePermission(string permissionKey, string? caveId, string? countyId)
    {
        return await RequestUser.HasCavePermission(permissionKey, caveId, countyId, false);
    }
}