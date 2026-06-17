using System.Text.Json;
using System.Text.Json.Serialization;
using EPiServer;

namespace Avantibit.Optimizely.CustomSettings.Infrastructure;

/// <summary>
/// System.Text.Json converter for EPiServer.Url.
/// Serializes to/from a plain JSON string (the URL value).
/// </summary>
public class UrlJsonConverter : JsonConverter<Url>
{
    public override Url? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var value = reader.GetString();
        return string.IsNullOrEmpty(value) ? null : new Url(value);
    }

    public override void Write(Utf8JsonWriter writer, Url? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.ToString());
    }
}
