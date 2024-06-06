using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Planarian.Shared.Options;

public static class PlanarianJsonOptions
{
    public static JsonSerializerOptions Default { get; } = new( JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    
    public static JsonOptions DefaultJsonOptions { get; } = new();
}