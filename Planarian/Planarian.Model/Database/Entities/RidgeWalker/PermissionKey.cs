using Planarian.Library.Exceptions;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public static class PermissionKey
{
    public const string View = "View";
    public const string Manage = "Manage";
    
    public static void ValidateKey(string key)
    {
        if (key != View && key != Manage)
        {
            throw ApiExceptionDictionary.InvalidPermission;
        }
    }
}