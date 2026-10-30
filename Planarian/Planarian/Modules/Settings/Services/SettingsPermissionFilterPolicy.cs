using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Modules.Settings.Services;

public static class SettingsPermissionFilterPolicy
{
    public static void Validate(string? permissionKey)
    {
        if (permissionKey is null or PermissionKey.View or PermissionKey.Manager) return;

        throw ApiExceptionDictionary.BadRequest("Invalid cave permission filter.");
    }
}
