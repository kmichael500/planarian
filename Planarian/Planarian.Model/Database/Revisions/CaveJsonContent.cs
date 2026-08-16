using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Planarian.Model.Database.Revisions;

public static class CaveJsonContent
{
    public static string Normalize(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, document.RootElement);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string NormalizeFeatureCollection(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
            !string.Equals(type.GetString(), "FeatureCollection", StringComparison.Ordinal) ||
            !root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            throw new JsonException("Line plot GeoJSON must be a FeatureCollection with a features array.");

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, root);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Hash(string normalizedJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static (string NormalizedJson, string ContentHash) NormalizeAndHash(string json)
    {
        var normalized = Normalize(json);
        return (normalized, Hash(normalized));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
