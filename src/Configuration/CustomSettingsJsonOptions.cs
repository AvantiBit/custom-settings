using Avantibit.Optimizely.CustomSettings.Infrastructure;
using System.Text.Json;

namespace Avantibit.Optimizely.CustomSettings.Configuration;

/// <summary>
/// Shared JSON serializer options for custom settings serialization.
/// Includes converters for types not natively supported by System.Text.Json.
/// </summary>
internal static class CustomSettingsJsonOptions
{
    internal static readonly JsonSerializerOptions Default = new()
    {
        Converters =
        {
            new UrlJsonConverter(),
            new ContentReferenceJsonConverter()
        }
    };
}
