namespace Planarian.Modules.Tags.Models;

public static class TagKey
{
    public const string Default = "Default";
    public const string TripObjective = "TripObjective";
    public const string Photo = "Photo";

    public static bool IsValidTagKey(string tagKey)
    {
        return tagKey is Default or TripObjective or Photo;
    }
}