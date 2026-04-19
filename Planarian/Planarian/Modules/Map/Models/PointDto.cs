namespace Planarian.Modules.Map.Models;

public class PointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Name { get; set; }
    public bool IsCluster { get; set; } = false;
}