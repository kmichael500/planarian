namespace Planarian.Modules.Map.Services;

public class ClusterPoint
{
    public string Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Elevation { get; set; }
}

public class Cluster
{
    public List<ClusterPoint> ClusterPoints { get; set; } = new List<ClusterPoint>();
}