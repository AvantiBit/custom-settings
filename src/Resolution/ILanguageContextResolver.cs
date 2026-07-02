namespace Avantibit.Optimizely.CustomSettings.Resolution
{
    /// <summary>
    /// Provides methods to resolve the current language context from Optimizely CMS.
    /// </summary>
    public interface ILanguageContextResolver
    {
        /// <summary>
        /// Gets the current language code from the request context.
        /// </summary>
        /// <returns>The current language code, or null if unable to resolve.</returns>
        string? GetCurrentLanguage();

        /// <summary>
        /// Gets the current language code or returns a default/fallback value when no language context is available.
        /// </summary>
        /// <returns>The current language code or the master language as default.</returns>
        string GetCurrentLanguageOrDefault();

        /// <summary>
        /// Gets all available languages for the current site.
        /// </summary>
        /// <returns>A collection of language codes for enabled languages.</returns>
        IEnumerable<string> GetAvailableLanguages();

        /// <summary>
        /// Gets the master/default language code for the current site.
        /// </summary>
        /// <returns>The master language code, or 'en' as fallback.</returns>
        string GetMasterLanguage();

        /// <summary>
        /// Gets available languages for a specific site.
        /// </summary>
        /// <param name="siteId">The site ID.</param>
        /// <returns>A collection of language codes available for the specified site.</returns>
        IEnumerable<string> GetAvailableLanguagesForSite(Guid siteId);
    }
}