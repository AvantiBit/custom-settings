using EPiServer;
using EPiServer.Core;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Avantibit.Optimizely.CustomSettings.Schema;

/// <summary>
/// Converts discovered setting classes into JSON Schema for UI rendering.
/// </summary>
public class SettingsSchemaBuilder : ISettingsSchemaBuilder
{
    private const string JsonSchemaVersion = "http://json-schema.org/draft-07/schema#";
    private readonly ILogger<SettingsSchemaBuilder> _logger;

    public SettingsSchemaBuilder(ILogger<SettingsSchemaBuilder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a JSON Schema from a settings type.
    /// </summary>
    public JsonNode BuildSchema(Type settingsType)
    {
        if (settingsType is null)
        {
            throw new ArgumentNullException(nameof(settingsType));
        }

        try
        {
            var schema = new JsonObject
            {
                ["$schema"] = JsonSchemaVersion,
                ["type"] = "object",
                ["title"] = GetTypeDisplayName(settingsType),
                ["description"] = GetTypeDescription(settingsType),
                ["properties"] = new JsonObject(),
                ["required"] = new JsonArray()
            };

            var properties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .OrderBy(p => GetPropertyOrder(p));

            var requiredProperties = new List<string>();

            foreach (var property in properties)
            {
                try
                {
                    var propertySchema = BuildPropertySchema(property);
                    schema["properties"]![property.Name] = propertySchema;

                    if (IsPropertyRequired(property))
                    {
                        requiredProperties.Add(property.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to build schema for property {PropertyName} on type {TypeName}. Property will be skipped.",
                        property.Name,
                        settingsType.FullName);
                }
            }

            if (requiredProperties.Count > 0)
            {
                var requiredArray = schema["required"]!.AsArray();
                foreach (var propertyName in requiredProperties)
                {
                    requiredArray.Add(propertyName);
                }
            }
            else
            {
                schema.Remove("required");
            }

            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to build schema for type {TypeName}. Returning minimal fallback schema.",
                settingsType.FullName);

            return new JsonObject
            {
                ["$schema"] = JsonSchemaVersion,
                ["type"] = "object",
                ["title"] = settingsType.Name,
                ["description"] = "Schema generation failed. Please check the settings class definition.",
                ["properties"] = new JsonObject()
            };
        }
    }

    /// <summary>
    /// Builds schema for a single property.
    /// </summary>
    private JsonNode BuildPropertySchema(PropertyInfo property)
    {
        var propertySchema = new JsonObject();

        var displayAttr = property.GetCustomAttribute<DisplayAttribute>();
        if (displayAttr is not null)
        {
            if (!string.IsNullOrWhiteSpace(displayAttr.Name))
            {
                propertySchema["title"] = displayAttr.Name;
            }

            if (!string.IsNullOrWhiteSpace(displayAttr.Description))
            {
                propertySchema["description"] = displayAttr.Description;
            }

            if (!string.IsNullOrWhiteSpace(displayAttr.Prompt))
            {
                propertySchema["placeholder"] = displayAttr.Prompt;
            }
        }

        if (!propertySchema.ContainsKey("description"))
        {
            var descAttr = property.GetCustomAttribute<DescriptionAttribute>();
            if (descAttr is not null && !string.IsNullOrWhiteSpace(descAttr.Description))
            {
                propertySchema["description"] = descAttr.Description;
            }
        }

        var fallbackAttr = property.GetCustomAttribute<Attributes.FallbackToMasterLanguageAttribute>();
        if (fallbackAttr is not null)
        {
            propertySchema["hasFallback"] = true;
        }

        var defaultAttr = property.GetCustomAttribute<DefaultValueAttribute>();
        if (defaultAttr is not null && defaultAttr.Value is not null)
        {
            propertySchema["default"] = JsonValue.Create(defaultAttr.Value);
        }

        var uiHintAttr = property.GetCustomAttribute<UIHintAttribute>();
        if (uiHintAttr is not null && !string.IsNullOrWhiteSpace(uiHintAttr.UIHint))
        {
            propertySchema["format"] = uiHintAttr.UIHint.ToLowerInvariant();
        }

        MapPropertyType(property, propertySchema);

        ApplyValidationAttributes(property, propertySchema);

        return propertySchema;
    }

    /// <summary>
    /// Maps a property type to JSON Schema type information.
    /// </summary>
    private void MapPropertyType(PropertyInfo property, JsonObject schema)
    {
        var propertyType = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (!IsAllowedType(underlyingType))
        {
            throw new InvalidOperationException(
                $"Property '{property.Name}' in type '{property.DeclaringType?.Name}' has unsupported type '{underlyingType.Name}'. " +
                "Supported types: string, int, bool, DateTime, DateTimeOffset, Guid, Uri, IList<string> (including nullable variants).");
        }

        var typeSchema = BuildTypeSchema(underlyingType);
        foreach (var kvp in typeSchema.AsObject())
        {
            schema[kvp.Key] = kvp.Value?.DeepClone();
        }
    }

    /// <summary>
    /// Builds type schema for simple types.
    /// </summary>
    private JsonObject BuildTypeSchema(Type type)
    {
        var schema = new JsonObject();
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string))
        {
            schema["type"] = "string";
        }
        else if (underlyingType == typeof(int))
        {
            schema["type"] = "integer";
        }
        else if (underlyingType == typeof(bool))
        {
            schema["type"] = "boolean";
        }
        else if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
        {
            schema["type"] = "string";
            schema["format"] = "date-time";
        }
        else if (underlyingType == typeof(Guid))
        {
            schema["type"] = "string";
            schema["format"] = "uuid";
        }
        else if (underlyingType == typeof(Uri))
        {
            schema["type"] = "string";
            schema["format"] = "uri";
        }
        else if (underlyingType == typeof(Url))
        {
            schema["type"] = "string";
            schema["format"] = "url-picker";
        }
        else if (underlyingType == typeof(ContentReference))
        {
            schema["type"] = "object";
            schema["format"] = "page-reference";
        }
        else if (IsStringListType(underlyingType))
        {
            schema["type"] = "array";
            schema["items"] = new JsonObject { ["type"] = "string" };
        }
        else
        {
            throw new InvalidOperationException(
                $"Type '{underlyingType.Name}' is not supported. " +
                "Supported types: string, int, bool, DateTime, DateTimeOffset, Guid, Uri, EPiServer.Url, ContentReference, IList<string>.");
        }

        return schema;
    }

    /// <summary>
    /// Applies validation attributes to the property schema.
    /// </summary>
    private void ApplyValidationAttributes(PropertyInfo property, JsonObject schema)
    {
        var stringLengthAttr = property.GetCustomAttribute<StringLengthAttribute>();
        if (stringLengthAttr is not null)
        {
            if (stringLengthAttr.MaximumLength > 0)
            {
                schema["maxLength"] = stringLengthAttr.MaximumLength;
            }

            if (stringLengthAttr.MinimumLength > 0)
            {
                schema["minLength"] = stringLengthAttr.MinimumLength;
            }
        }

        var minLengthAttr = property.GetCustomAttribute<MinLengthAttribute>();
        if (minLengthAttr is not null && minLengthAttr.Length > 0)
        {
            schema["minLength"] = minLengthAttr.Length;
        }

        var maxLengthAttr = property.GetCustomAttribute<MaxLengthAttribute>();
        if (maxLengthAttr is not null && maxLengthAttr.Length > 0)
        {
            schema["maxLength"] = maxLengthAttr.Length;
        }

        var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
        if (rangeAttr is not null)
        {
            if (rangeAttr.Minimum is not null)
            {
                schema["minimum"] = JsonValue.Create(rangeAttr.Minimum);
            }

            if (rangeAttr.Maximum is not null)
            {
                schema["maximum"] = JsonValue.Create(rangeAttr.Maximum);
            }
        }

        var regexAttr = property.GetCustomAttribute<RegularExpressionAttribute>();
        if (regexAttr is not null && !string.IsNullOrWhiteSpace(regexAttr.Pattern))
        {
            schema["pattern"] = regexAttr.Pattern;
        }

        var emailAttr = property.GetCustomAttribute<EmailAddressAttribute>();
        if (emailAttr is not null)
        {
            schema["format"] = "email";
        }

        var urlAttr = property.GetCustomAttribute<UrlAttribute>();
        if (urlAttr is not null)
        {
            schema["format"] = "uri";
        }

        var phoneAttr = property.GetCustomAttribute<PhoneAttribute>();
        if (phoneAttr is not null)
        {
            schema["format"] = "phone";
        }

        var creditCardAttr = property.GetCustomAttribute<CreditCardAttribute>();
        if (creditCardAttr is not null)
        {
            schema["format"] = "credit-card";
        }

        var compareAttr = property.GetCustomAttribute<CompareAttribute>();
        if (compareAttr is not null && !string.IsNullOrWhiteSpace(compareAttr.OtherProperty))
        {
            if (!schema.ContainsKey("x-validation"))
            {
                schema["x-validation"] = new JsonObject();
            }

            schema["x-validation"]!["compare"] = compareAttr.OtherProperty;
        }
    }

    /// <summary>
    /// Checks if a property is required.
    /// </summary>
    private bool IsPropertyRequired(PropertyInfo property)
    {
        if (property.GetCustomAttribute<RequiredAttribute>() != null)
        {
            return true;
        }

        var propertyType = property.PropertyType;
        if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the display name for a type.
    /// </summary>
    private string GetTypeDisplayName(Type type)
    {
        var displayAttr = type.GetCustomAttribute<DisplayAttribute>();
        if (displayAttr is not null && !string.IsNullOrWhiteSpace(displayAttr.Name))
        {
            return displayAttr.Name;
        }

        var displayNameAttr = type.GetCustomAttribute<DisplayNameAttribute>();
        if (displayNameAttr is not null && !string.IsNullOrWhiteSpace(displayNameAttr.DisplayName))
        {
            return displayNameAttr.DisplayName;
        }

        return type.Name;
    }

    /// <summary>
    /// Gets the description for a type.
    /// </summary>
    private string GetTypeDescription(Type type)
    {
        var displayAttr = type.GetCustomAttribute<DisplayAttribute>();
        if (displayAttr is not null && !string.IsNullOrWhiteSpace(displayAttr.Description))
        {
            return displayAttr.Description;
        }

        var descriptionAttr = type.GetCustomAttribute<DescriptionAttribute>();
        if (descriptionAttr is not null && !string.IsNullOrWhiteSpace(descriptionAttr.Description))
        {
            return descriptionAttr.Description;
        }

        return string.Empty;
    }

    /// <summary>
    /// Gets the display order for a property.
    /// </summary>
    private int GetPropertyOrder(PropertyInfo property)
    {
        var displayAttr = property.GetCustomAttribute<DisplayAttribute>();
        return displayAttr?.GetOrder() ?? int.MaxValue;
    }

    private static bool IsAllowedType(Type type)
    {
        return type == typeof(string) ||
               type == typeof(int) ||
               type == typeof(bool) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(Guid) ||
               type == typeof(Uri) ||
               type == typeof(Url) ||
               type == typeof(ContentReference) ||
               IsStringListType(type);
    }

    private static bool IsStringListType(Type type)
    {
        if (!type.IsGenericType) return false;
        var def = type.GetGenericTypeDefinition();
        return (def == typeof(List<>) || def == typeof(IList<>) || def == typeof(IEnumerable<>))
            && type.GetGenericArguments()[0] == typeof(string);
    }
}