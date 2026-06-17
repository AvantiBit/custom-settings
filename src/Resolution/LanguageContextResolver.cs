using EPiServer.DataAbstraction;
using EPiServer.Globalization;
using EPiServer.ServiceLocation;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Avantibit.Optimizely.CustomSettings.Resolution;

/// <summary>
/// Resolves the current language context from Optimizely CMS.
/// Integrates with Optimizely's ContentLanguage and ILanguageBranchRepository.
/// </summary>
[ServiceConfiguration(typeof(ILanguageContextResolver), Lifecycle = ServiceInstanceScope.Singleton)]
public class LanguageContextResolver : ILanguageContextResolver
{
    private readonly ILanguageBranchRepository _languageBranchRepository;
    private readonly ILogger<LanguageContextResolver> _logger;

    public LanguageContextResolver(
        ILanguageBranchRepository languageBranchRepository,
        ILogger<LanguageContextResolver> logger)
    {
        _languageBranchRepository = languageBranchRepository ?? throw new ArgumentNullException(nameof(languageBranchRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string? GetCurrentLanguage()
    {
        try
        {
            var contentLanguage = ContentLanguage.PreferredCulture;

            if (contentLanguage is not null && !string.IsNullOrWhiteSpace(contentLanguage.Name))
            {
                var languageCode = contentLanguage.TwoLetterISOLanguageName;

                _logger.LogDebug(
                    "Resolved language from ContentLanguage: {LanguageCode} (Culture: {CultureName})",
                    languageCode,
                    contentLanguage.Name);

                return languageCode;
            }

            var currentCulture = CultureInfo.CurrentUICulture;
            if (currentCulture is not null && !string.IsNullOrWhiteSpace(currentCulture.Name))
            {
                var languageCode = currentCulture.TwoLetterISOLanguageName;

                _logger.LogDebug(
                    "Resolved language from current UI culture: {LanguageCode} (Culture: {CultureName})",
                    languageCode,
                    currentCulture.Name);

                return languageCode;
            }

            _logger.LogWarning("Unable to resolve current language from context");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving current language");
            return null;
        }
    }

    public string GetCurrentLanguageOrDefault()
    {
        var language = GetCurrentLanguage();

        if (!string.IsNullOrWhiteSpace(language))
            return language;

        return GetMasterLanguage();
    }

    public IEnumerable<string> GetAvailableLanguages()
    {
        try
        {
            var allLanguages = _languageBranchRepository
                .ListEnabled()
                .Select(l => l.Culture.TwoLetterISOLanguageName)
                .Distinct()
                .ToList();

            if (allLanguages.Count > 0)
            {
                _logger.LogDebug(
                    "Resolved {Count} enabled languages: {Languages}",
                    allLanguages.Count,
                    string.Join(", ", allLanguages));

                return allLanguages;
            }

            _logger.LogWarning("No enabled languages found, returning default 'en'");
            return ["en"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available languages");
            return ["en"];
        }
    }

    public string GetMasterLanguage()
    {
        try
        {
            var firstEnabledLanguage = _languageBranchRepository.ListEnabled().FirstOrDefault();

            if (firstEnabledLanguage is not null)
            {
                var languageCode = firstEnabledLanguage.Culture.TwoLetterISOLanguageName;

                _logger.LogDebug(
                    "Resolved master language: {LanguageCode} (Culture: {CultureName})",
                    languageCode,
                    firstEnabledLanguage.Culture.Name);

                return languageCode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error accessing master language");
        }

        _logger.LogDebug("No language branches available, falling back to 'en'");
        return "en";
    }

    public IEnumerable<string> GetAvailableLanguagesForSite(Guid siteId)
    {
        // In CMS 13, language configuration is global (ILanguageBranchRepository).
        // Site-specific language filtering via SiteDefinition is no longer available.
        return GetAvailableLanguages();
    }
}