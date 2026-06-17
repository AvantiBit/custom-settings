using Avantibit.Optimizely.CustomSettings.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Avantibit.Optimizely.CustomSettings.Validation;

/// <summary>
/// Factory for creating, validating, and mapping Settings ViewModels.
/// Uses strongly-typed instances and built-in .NET validation.
/// </summary>
public interface ISettingsViewModelFactory
{
    /// <summary>
    /// Creates a ViewModel instance from JSON data
    /// </summary>
    object CreateViewModel(Type settingsType, JsonNode? data);

    /// <summary>
    /// Validates a ViewModel using Data Annotations
    /// </summary>
    bool ValidateViewModel(object viewModel, out Dictionary<string, List<string>> errors);

    /// <summary>
    /// Maps a ViewModel back to JSON for storage
    /// </summary>
    JsonNode MapToJson(object viewModel);
}

public class SettingsViewModelFactory : ISettingsViewModelFactory
{
    private readonly ILogger<SettingsViewModelFactory> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = CustomSettingsJsonOptions.Default;

    public SettingsViewModelFactory(ILogger<SettingsViewModelFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a strongly-typed ViewModel instance and populates it from JSON
    /// </summary>
    public object CreateViewModel(Type settingsType, JsonNode? data)
    {
        if (settingsType == null) throw new ArgumentNullException(nameof(settingsType));

        if (data == null)
        {
            return Activator.CreateInstance(settingsType)
                ?? throw new InvalidOperationException($"Cannot create instance of {settingsType.Name}");
        }

        return JsonSerializer.Deserialize(data.ToJsonString(), settingsType, _jsonOptions)
            ?? Activator.CreateInstance(settingsType)
            ?? throw new InvalidOperationException($"Cannot create instance of {settingsType.Name}");
    }

    /// <summary>
    /// Validates ViewModel using Data Annotations validator
    /// </summary>
    public bool ValidateViewModel(object viewModel, out Dictionary<string, List<string>> errors)
    {
        if (viewModel == null)
        {
            throw new ArgumentNullException(nameof(viewModel));
        }

        errors = new Dictionary<string, List<string>>();

        // Create validation context with the actual instance
        var validationContext = new ValidationContext(viewModel)
        {
            MemberName = null // Validates entire object
        };

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            viewModel,
            validationContext,
            validationResults,
            validateAllProperties: true);

        if (!isValid)
        {
            // Map validation results to error dictionary
            foreach (var validationResult in validationResults)
            {
                var errorMessage = validationResult.ErrorMessage ?? "Validation error";

                // Handle both property-level and object-level errors
                if (validationResult.MemberNames?.Any() == true)
                {
                    foreach (var memberName in validationResult.MemberNames)
                    {
                        if (!errors.ContainsKey(memberName))
                        {
                            errors[memberName] = new List<string>();
                        }
                        errors[memberName].Add(errorMessage);
                    }
                }
                else
                {
                    // Object-level validation error (from IValidatableObject)
                    if (!errors.ContainsKey("_general"))
                    {
                        errors["_general"] = new List<string>();
                    }
                    errors["_general"].Add(errorMessage);
                }
            }

            _logger.LogDebug(
                "Validation failed for {TypeName} with {ErrorCount} errors: {Errors}",
                viewModel.GetType().Name,
                errors.Count,
                JsonSerializer.Serialize(errors));
        }

        return isValid;
    }

    /// <summary>
    /// Maps ViewModel properties back to JSON for storage
    /// </summary>
    public JsonNode MapToJson(object viewModel)
    {
        if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));

        return JsonSerializer.SerializeToNode(viewModel, _jsonOptions)
            ?? new JsonObject();
    }
}