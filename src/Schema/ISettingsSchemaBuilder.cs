using System.Text.Json.Nodes;

namespace Avantibit.Optimizely.CustomSettings.Schema;

/// <summary>
/// Interface for building JSON Schema from settings types.
/// </summary>
public interface ISettingsSchemaBuilder
{
    /// <summary>
    /// Generates a JSON Schema from a settings type.
    /// </summary>
    JsonNode BuildSchema(Type settingsType);
}