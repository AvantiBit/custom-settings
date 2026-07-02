namespace Avantibit.Optimizely.CustomSettings.Resolution
{
    /// <summary>
    /// Provides methods to resolve the current site context from Optimizely CMS.
    /// </summary>
    public interface ISiteContextResolver
    {
        /// <summary>
        /// Gets the current site ID from the request context.
        /// </summary>
        /// <returns>The current site ID, or null if unable to resolve.</returns>
        Guid? GetCurrentSiteId();

        /// <summary>
        /// Gets the current site ID or returns a default/fallback value when no site context is available.
        /// </summary>
        /// <returns>The current site ID or Guid.Empty as fallback.</returns>
        Guid GetCurrentSiteIdOrDefault();

        /// <summary>
        /// Gets the current site name.
        /// </summary>
        /// <returns>The current site name, or null if unable to resolve.</returns>
        string? GetCurrentSiteName();
    }
}