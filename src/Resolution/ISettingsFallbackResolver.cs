namespace Avantibit.Optimizely.CustomSettings.Resolution;

/// <summary>
/// Provides fallback resolution for settings properties decorated with [FallbackToMasterLanguage].
/// </summary>
public interface ISettingsFallbackResolver
{
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
    Task<T?> ApplyFallbackAsync<T>(
        T? settings,
        Guid? siteId,
        string? languageCode,
        CancellationToken cancellationToken = default) where T : class, new();
}