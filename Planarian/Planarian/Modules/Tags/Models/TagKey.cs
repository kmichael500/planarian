namespace Planarian.Modules.Tags.Models;

public static class TagKey
{
    public const string Default = "Default";
    public const string TripObjective = "TripObjective";
    public const string Photo = "Photo";
    
    private static readonly List<string> All = new() {Default, TripObjective, Photo};
    public static bool IsValidTagKey(string tagKey)
    {
        return All.Any(e=>e == tagKey);
    }
}