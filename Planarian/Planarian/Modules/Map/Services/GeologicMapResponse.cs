using System.Text.Json.Serialization;

namespace Planarian.Modules.Map.Services;

public class GeologicMapResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("previous")]
    public string? Previous { get; set; }

    [JsonPropertyName("results")]
    public List<GeologicMapResult> Results { get; set; } = new List<GeologicMapResult>();
}