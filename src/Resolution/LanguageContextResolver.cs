using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Globalization;
using EPiServer.ServiceLocation;
using EPiServer.Web;
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
    private readonly ISiteDefinitionRepository _siteDefinitionRepository;
    private readonly IContentRepository _contentRepository;

    public LanguageContextResolver(
        ILanguageBranchRepository languageBranchRepository,
        ILogger<LanguageContextResolver> logger,
        ISiteDefinitionRepository siteDefinitionRepository,
        IContentRepository contentRepository)
    {
        _languageBranchRepository = languageBranchRepository ?? throw new ArgumentNullException(nameof(languageBranchRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _siteDefinitionRepository = siteDefinitionRepository ?? throw new ArgumentNullException(nameof(siteDefinitionRepository));
        _contentRepository = contentRepository ?? throw new ArgumentNullException(nameof(contentRepository));
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
        {
            return language;
        }

        return GetMasterLanguage();
    }

    /// <summary>
    /// Gets all available languages for the current site.
    /// </summary>
    /// <returns>A collection of language codes for enabled languages.</returns>
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
            return new[] { "en" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available languages");
            return new[] { "en" };
        }
    }

    /// <summary>
    /// Gets the master/default language code for the current site.
    /// </summary>
    /// <returns>The master language code, or 'en' as fallback.</returns>
    public string GetMasterLanguage()
    {
        try
        {
            var enabledLanguages = _languageBranchRepository.ListEnabled();
            var firstEnabledLanguage = enabledLanguages.FirstOrDefault();

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

    /// <summary>
    /// Gets available languages for a specific site.
    /// </summary>
    /// <param name="siteId">The site ID.</param>
    /// <returns>A collection of language codes available for the specified site.</returns>
    public IEnumerable<string> GetAvailableLanguagesForSite(Guid siteId)
    {
        try
        {
            var site = _siteDefinitionRepository.Get(siteId);
            if (site == null)
            {
                _logger.LogWarning("Site not found: {SiteId}", siteId);
                return GetAvailableLanguages();
            }

            _logger.LogInformation("Checking languages for site {SiteId} ({SiteName})", siteId, site.Name);

            // Try to get languages from the site's start page
            if (site.StartPage != null && !ContentReference.IsNullOrEmpty(site.StartPage))
            {
                try
                {
                    var languageBranches = _contentRepository.GetLanguageBranches<IContent>(site.StartPage);
                    var existingLanguages = languageBranches
                        .OfType<ILocalizable>()
                        .Where(l => l.Language != null)
                        .Select(l => l.Language.TwoLetterISOLanguageName)
                        .Distinct()
                        .ToList();

                    if (existingLanguages.Count > 0)
                    {
                        _logger.LogInformation(
                            "Found {Count} languages for site {SiteId} from start page: {Languages}",
                            existingLanguages.Count,
                            siteId,
                            string.Join(", ", existingLanguages));

                        return existingLanguages;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load start page for site {SiteId}", siteId);
                }
            }

            var hostLanguages = site.Hosts
                .Where(h => h.Language != null && !string.IsNullOrEmpty(h.Language.Name))
                .Select(h => h.Language.Name)
                .Distinct()
                .ToList();

            if (hostLanguages.Count > 0)
            {
                _logger.LogInformation(
                    "Found {Count} languages for site {SiteId} from host definitions: {Languages}",
                    hostLanguages.Count,
                    siteId,
                    string.Join(", ", hostLanguages));

                return hostLanguages;
            }

            _logger.LogInformation(
                "No site-specific languages found for site {SiteId}, returning all enabled languages",
                siteId);

            return GetAvailableLanguages();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving languages for site {SiteId}", siteId);
            return GetAvailableLanguages();
        }
    }
}