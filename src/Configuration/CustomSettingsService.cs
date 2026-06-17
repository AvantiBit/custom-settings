using System.Text.Json;
using Avantibit.Optimizely.CustomSettings.Caching;
using Avantibit.Optimizely.CustomSettings.Persistence.Abstractions;
using Avantibit.Optimizely.CustomSettings.Resolution;
using Microsoft.Extensions.Logging;

namespace Avantibit.Optimizely.CustomSettings.Configuration;

/// <summary>
/// Generic service implementation for typed access to custom settings with in-memory caching.
/// </summary>
/// <typeparam name="T">The type of settings to manage.</typeparam>
public class CustomSettingsService<T> : ICustomSettingsService<T> where T : class, new()
{
    private readonly ISettingsRepository _repository;
    private readonly ISettingsCacheService _cacheService;
    private readonly ISiteContextResolver _siteContextResolver;
    private readonly ILanguageContextResolver _languageContextResolver;
    private readonly ILogger<CustomSettingsService<T>> _logger;

    public CustomSettingsService(
        ISettingsRepository repository,
        ISettingsCacheService cacheService,
        ISiteContextResolver siteContextResolver,
        ILanguageContextResolver languageContextResolver,
        ILogger<CustomSettingsService<T>> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _siteContextResolver = siteContextResolver ?? throw new ArgumentNullException(nameof(siteContextResolver));
        _languageContextResolver = languageContextResolver ?? throw new ArgumentNullException(nameof(languageContextResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    }

    /// <summary>
    /// Retrieves settings for a specific site and language from the in-memory cache.
    /// </summary>
    public Task<T> GetAsync(
        Guid? siteId = null,
        string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedSiteId = siteId ?? _siteContextResolver.GetCurrentSiteIdOrDefault();
            var resolvedLanguageCode = languageCode ?? _languageContextResolver.GetCurrentLanguageOrDefault();

            return Task.FromResult(_cacheService.Get<T>(resolvedSiteId, resolvedLanguageCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode}). Returning default instance.",
                typeof(T).Name,
                siteId,
                languageCode);

            return Task.FromResult(new T());
        }
    }

    /// <summary>
    /// Saves settings for a specific site and language, then refreshes the local cache.
    /// </summary>
    public async Task SaveAsync(
        T settings,
        Guid? siteId = null,
        string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var resolvedSiteId = siteId ?? _siteContextResolver.GetCurrentSiteIdOrDefault();
            var resolvedLanguageCode = languageCode ?? _languageContextResolver.GetCurrentLanguageOrDefault();

            var entity = new SettingsEntity
            {
                SettingsType = typeof(T).FullName!,
                SiteId = resolvedSiteId,
                LanguageCode = resolvedLanguageCode,
                JsonData = JsonSerializer.Serialize(settings, CustomSettingsJsonOptions.Default),
                ModifiedUtc = DateTime.UtcNow
            };

            await _repository.SaveAsync(entity, cancellationToken);
            await _cacheService.LoadAllAsync(cancellationToken);




            _logger.LogInformation(
                "Saved settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode})",
                typeof(T).Name,
                resolvedSiteId,
                resolvedLanguageCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to save settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode})",
                typeof(T).Name,
                siteId,
                languageCode);
            throw;
        }
    }

    /// <summary>
    /// Deletes settings for a specific site and language, then refreshes the local cache.
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid? siteId = null,
        string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedSiteId = siteId ?? _siteContextResolver.GetCurrentSiteIdOrDefault();
            var resolvedLanguageCode = languageCode ?? _languageContextResolver.GetCurrentLanguageOrDefault();

            var deleted = await _repository.DeleteAsync(
                typeof(T).FullName!,
                resolvedSiteId,
                resolvedLanguageCode,
                cancellationToken);

            if (deleted)
            {
                await _cacheService.LoadAllAsync(cancellationToken);

                _logger.LogInformation(
                    "Deleted settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode})",
                    typeof(T).Name,
                    resolvedSiteId,
                    resolvedLanguageCode);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete settings {SettingsType} (Site: {SiteId}, Language: {LanguageCode})",
                typeof(T).Name,
                siteId, languageCode);
            throw;
        }
    }
}
