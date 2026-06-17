using Avantibit.Optimizely.CustomSettings.Attributes;
using Avantibit.Optimizely.CustomSettings.Configuration;
using Avantibit.Optimizely.CustomSettings.Infrastructure;
using Avantibit.Optimizely.CustomSettings.Persistence.Abstractions;
using EPiServer.ServiceLocation;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace Avantibit.Optimizely.CustomSettings.Resolution;

/// <summary>
/// Resolves property-level fallback to master language for settings decorated with [FallbackToMasterLanguage].
/// </summary>
[ServiceConfiguration(typeof(ISettingsFallbackResolver), Lifecycle = ServiceInstanceScope.Singleton)]
public class SettingsFallbackResolver : ISettingsFallbackResolver
{
    private readonly ISettingsRepository _repository;
    private readonly ILanguageContextResolver _languageContextResolver;
    private readonly ILogger<SettingsFallbackResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsFallbackResolver"/> class.
    /// </summary>
    /// <param name="repository">The settings repository.</param>
    /// <param name="languageContextResolver">The language context resolver.</param>
    /// <param name="logger">The logger instance.</param>
    public SettingsFallbackResolver(
        ISettingsRepository repository,
        ILanguageContextResolver languageContextResolver,
        ILogger<SettingsFallbackResolver> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _languageContextResolver = languageContextResolver ?? throw new ArgumentNullException(nameof(languageContextResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Applies fallback logic to settings, populating properties marked with [FallbackToMasterLanguage]
    /// from master language when current language values are null or default.
    /// </summary>
    /// <typeparam name="T">The settings type. Must be a reference type with a parameterless constructor.</typeparam>
    /// <param name="settings">The settings instance to apply fallback to.</param>
    /// <param name="siteId">The site ID.</param>
    /// <param name="languageCode">The language code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings with fallback values applied.</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// <list type="number">
    /// <item>Returns immediately if settings is null</item>
    /// <item>Returns unchanged settings if the current language is already the master language</item>
    /// <item>Identifies all properties decorated with [FallbackToMasterLanguage] attribute</item>
    /// <item>Loads master language settings from repository</item>
    /// <item>For each fallback property, replaces null/default values with master language values</item>
    /// </list>
    /// This enables multi-language support where optional fields can fall back to the master language
    /// when not explicitly set for a specific language.
    /// </remarks>
    public async Task<T?> ApplyFallbackAsync<T>(
        T? settings,
        Guid? siteId,
        string? languageCode,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        if (settings == null)
        {
            return settings;
        }

        var masterLanguage = _languageContextResolver.GetMasterLanguage();

        if (string.Equals(languageCode, masterLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return settings;
        }

        var type = typeof(T);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<FallbackToMasterLanguageAttribute>() != null)
            .ToList();

        if (properties.Count == 0)
        {
            return settings;
        }

        _logger.LogDebug(
            "Found {Count} properties with [FallbackToMasterLanguage] on {SettingsType}",
            properties.Count,
            type.Name);

        var masterSettings = await LoadMasterLanguageSettingsAsync<T>(siteId, masterLanguage, cancellationToken);

        if (masterSettings is null)
        {
            _logger.LogDebug(
                "No master language settings found for {SettingsType} (Site: {SiteId}, MasterLanguage: {MasterLanguage})",
                type.Name,
                siteId,
                masterLanguage);
            return settings;
        }

        foreach (var property in properties)
        {
            try
            {
                var currentValue = property.GetValue(settings);

                if (SettingsValueHelper.IsNullOrDefault(currentValue))
                {
                    var masterValue = property.GetValue(masterSettings);

                    if (!SettingsValueHelper.IsNullOrDefault(masterValue))
                    {
                        property.SetValue(settings, masterValue);

                        _logger.LogDebug(
                            "Applied fallback for property {PropertyName} on {SettingsType} (Language: {Language} → {MasterLanguage})",
                            property.Name,
                            type.Name,
                            languageCode,
                            masterLanguage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to apply fallback for property {PropertyName} on {SettingsType}",
                    property.Name,
                    type.Name);
            }
        }

        return settings;
    }

    /// <summary>
    /// Loads master language settings from the repository.
    /// </summary>
    /// <typeparam name="T">The settings type.</typeparam>
    /// <param name="siteId">The site ID.</param>
    /// <param name="masterLanguage">The master language code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The master language settings, or null if not found.</returns>
    private async Task<T?> LoadMasterLanguageSettingsAsync<T>(
        Guid? siteId,
        string masterLanguage,
        CancellationToken cancellationToken) where T : class, new()
    {
        try
        {
            var entity = await _repository.GetAsync(
                typeof(T).FullName!,
                siteId,
                masterLanguage,
                cancellationToken);

            if (entity == null)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(entity.JsonData, CustomSettingsJsonOptions.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error loading master language settings for {SettingsType} (Site: {SiteId}, Language: {MasterLanguage})",
                typeof(T).Name,
                siteId,
                masterLanguage);
            return null;
        }
    }
}