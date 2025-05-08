using System.Text.Json.Serialization;

namespace Planarian.Modules.Map.Services;

public class GeologicMapResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("authors")]
    public string Authors { get; set; } = "";

    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = "";

    [JsonPropertyName("series")]
    public string Series { get; set; } = "";

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("scale")]
    public int Scale { get; set; }

    [JsonPropertyName("include")]
    public int Include { get; set; }

    [JsonPropertyName("bed_surf")]
    public int BedSurf { get; set; }

    [JsonPropertyName("gis")]
    public int? Gis { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("north")]
    public string North { get; set; } = "";

    [JsonPropertyName("south")]
    public string South { get; set; } = "";

    [JsonPropertyName("east")]
    public string East { get; set; } = "";

    [JsonPropertyName("west")]
    public string West { get; set; } = "";

    [JsonPropertyName("mv")]
    public int? Mv { get; set; }
}