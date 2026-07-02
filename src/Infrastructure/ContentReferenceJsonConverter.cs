using EPiServer.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Avantibit.Optimizely.CustomSettings.Infrastructure;

/// <summary>
/// System.Text.Json converter for EPiServer.Core.ContentReference.
/// Serializes to/from {"id": N, "workId": N, "providerName": "..."}.
/// ContentReference.EmptyReference (ID=0) is treated as JSON null.
/// </summary>
public class ContentReferenceJsonConverter : JsonConverter<ContentReference>
{
    public override ContentReference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            return ContentReference.EmptyReference;

        int id = 0;
        int workId = 0;
        string? providerName = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propName = reader.GetString();
                reader.Read();
                switch (propName?.ToLowerInvariant())
                {
                    case "id":
                        id = reader.TokenType == JsonTokenType.Number ? reader.GetInt32() : 0;
                        break;
                    case "workid":
                        workId = reader.TokenType == JsonTokenType.Number ? reader.GetInt32() : 0;
                        break;
                    case "providername":
                        providerName = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
        }

        if (id == 0)
        {
            return ContentReference.EmptyReference;
        }

        return new ContentReference(id, workId, providerName);
    }

    public override void Write(Utf8JsonWriter writer, ContentReference? value, JsonSerializerOptions options)
    {
        if (value == null || ContentReference.IsNullOrEmpty(value))
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteNumber("id", value.ID);
        writer.WriteNumber("workId", value.WorkID);
        if (value.ProviderName != null)
            writer.WriteString("providerName", value.ProviderName);
        else
            writer.WriteNull("providerName");
        writer.WriteEndObject();
    }
}
