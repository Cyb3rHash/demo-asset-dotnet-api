using System.Text.Json;
using System.Text.Json.Serialization;

namespace DemoAssetDotnetApi.Api;

/// <summary>
/// Shared JSON serializer options for the service.
/// </summary>
public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
