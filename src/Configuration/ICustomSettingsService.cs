namespace Avantibit.Optimizely.CustomSettings.Configuration;

/// <summary>
/// Generic service interface for typed access to custom settings with synchronous and asynchronous access patterns.
/// </summary>
/// <typeparam name="T">The type of settings to manage. Must be a reference type with a parameterless constructor.</typeparam>
public interface ICustomSettingsService<T> where T : class, new()
{
    /// <summary>
    /// Asynchronously retrieves settings for a specific site and language.
    /// </summary>
    /// <param name="siteId">The site ID, or null to use the current site context.</param>
    /// <param name="languageCode">The language code, or null to use the current language context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings instance, or a new default instance if not found.</returns>
    Task<T> GetAsync(Guid? siteId = null, string? languageCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously saves settings for a specific site and language.
    /// </summary>
    /// <param name="settings">The settings instance to save.</param>
    /// <param name="siteId">The site ID, or null to use the current site context.</param>
    /// <param name="languageCode">The language code, or null to use the current language context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SaveAsync(T settings, Guid? siteId = null, string? languageCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes settings for a specific site and language.
    /// </summary>
    /// <param name="siteId">The site ID, or null to use the current site context.</param>
    /// <param name="languageCode">The language code, or null to use the current language context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if settings were deleted, false if no settings existed.</returns>
    Task<bool> DeleteAsync(Guid? siteId = null, string? languageCode = null, CancellationToken cancellationToken = default);
}