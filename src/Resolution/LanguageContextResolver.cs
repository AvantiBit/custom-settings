using EPiServer;
using EPiServer.Applications;
using EPiServer.Core;
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
    private readonly IApplicationRepository _applicationRepository;
    private readonly IContentRepository _contentRepository;
    private readonly ILogger<LanguageContextResolver> _logger;

    public LanguageContextResolver(
        ILanguageBranchRepository languageBranchRepository,
        IApplicationRepository applicationRepository,
        IContentRepository contentRepository,
        ILogger<LanguageContextResolver> logger)
    {
        _languageBranchRepository = languageBranchRepository ?? throw new ArgumentNullException(nameof(languageBranchRepository));
        _applicationRepository = applicationRepository ?? throw new ArgumentNullException(nameof(applicationRepository));
        _contentRepository = contentRepository ?? throw new ArgumentNullException(nameof(contentRepository));
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

    /// <summary>
    /// Gets available languages for a specific site (application).
    /// </summary>
    /// <param name="siteId">The site ID (derived from the application name).</param>
    /// <returns>A collection of language codes available for the specified site.</returns>
    public IEnumerable<string> GetAvailableLanguagesForSite(Guid siteId)
    {
        try
        {
            var application = _applicationRepository.List()
                .FirstOrDefault(a => SiteContextResolver.GenerateApplicationId(a.Name) == siteId);

            if (application is null)
            {
                _logger.LogWarning("Application not found: {SiteId}", siteId);
                return GetAvailableLanguages();
            }

            _logger.LogInformation("Checking languages for application {SiteId} ({AppName})", siteId, application.Name);

            if (application is IRoutableApplication routable)
            {
                // Try to get languages from the application's entry point content
                if (!ContentReference.IsNullOrEmpty(routable.EntryPoint))
                {
                    try
                    {
                        var languageBranches = _contentRepository.GetLanguageBranches<IContent>(routable.EntryPoint);
                        var existingLanguages = languageBranches
                            .OfType<ILocalizable>()
                            .Where(l => l.Language != null)
                            .Select(l => l.Language.TwoLetterISOLanguageName)
                            .Distinct()
                            .ToList();

                        if (existingLanguages.Count > 0)
                        {
                            _logger.LogInformation(
                                "Found {Count} languages for application {SiteId} from entry point: {Languages}",
                                existingLanguages.Count,
                                siteId,
                                string.Join(", ", existingLanguages));

                            return existingLanguages;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not load entry point for application {SiteId}", siteId);
                    }
                }

                var hostLanguages = routable.Hosts
                    .Where(h => h.Locale != null)
                    .Select(h => h.Locale!.TwoLetterISOLanguageName)
                    .Distinct()
                    .ToList();

                if (hostLanguages.Count > 0)
                {
                    _logger.LogInformation(
                        "Found {Count} languages for application {SiteId} from host definitions: {Languages}",
                        hostLanguages.Count,
                        siteId,
                        string.Join(", ", hostLanguages));

                    return hostLanguages;
                }
            }

            _logger.LogInformation(
                "No application-specific languages found for {SiteId}, returning all enabled languages",
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