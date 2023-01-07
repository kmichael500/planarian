using Planarian.Model.Shared;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.Users.Services;

public class UserService : ServiceBase<UserRepository>
{
    public UserService(UserRepository repository, RequestUser requestUser) : base(repository, requestUser)
    {
    }
}