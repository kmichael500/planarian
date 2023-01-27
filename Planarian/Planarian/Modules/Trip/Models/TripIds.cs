namespace Planarian.Modules.Trips.Models;

public class TripIds
{
    public TripIds(string projectId, string tripId)
    {
        ProjectId = projectId;
        TripId = tripId;
    }

    public TripIds()
    {
    }

    public string ProjectId { get; set; } = null!;
    public string TripId { get; set; } = null!;
}