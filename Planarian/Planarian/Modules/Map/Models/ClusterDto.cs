using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;

namespace Planarian.Modules.Map.Controllers;

public class ClusterDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Count { get; set; }
    public bool IsCluster { get; set; } = true;
    public List<CoordinateDto> HullCoordinates { get; set; }
    public Geometry ConvexHull { get; set; }
    public IEnumerable<Point> g { get; set; }
    public ConvexHull c { get; set; }
    public Geometry a { get; set; }
}