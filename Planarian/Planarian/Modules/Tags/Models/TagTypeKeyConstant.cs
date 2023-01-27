namespace Planarian.Modules.Tags.Models;

public static class TagTypeKeyConstant
{
    public const string Default = "Default";
    public const string Trip = "Trip";
    public const string Photo = "Photo";

    private static readonly List<string> All = new() { Default, Trip, Photo };

    public static bool IsValidTagKey(string tagKey)
    {
        return All.Any(e => e == tagKey);
    }
}