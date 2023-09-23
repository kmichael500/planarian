using Planarian.Library.Options;
using Planarian.Model.Shared;
using Planarian.Modules.App.Models;
using Planarian.Modules.App.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.App.Services;

public class AppService : ServiceBase<AppRepository>
{
    private readonly ServerOptions _serverOptions;

    public AppService(AppRepository repository, RequestUser requestUser, ServerOptions serverOptions) : base(repository,
        requestUser)
    {
        _serverOptions = serverOptions;
    }

    public async Task<AppInitializeVm> Initialize(string serverBaseUrl)
    {
#if DEBUG
        _serverOptions.ServerBaseUrl = serverBaseUrl;
#endif
        var result = new AppInitializeVm
        {
            SignalrBaseUrl = $"{_serverOptions.ServerBaseUrl}/api/notificationHub"
        };
        return result;
    }
}