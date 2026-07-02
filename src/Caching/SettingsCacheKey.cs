namespace Avantibit.Optimizely.CustomSettings.Caching;

/// <summary>
/// Represents a unique cache key for settings storage, combines settings type, site ID, and language code.
/// </summary>
public readonly record struct SettingsCacheKey
{
    /// <summary>
    /// Gets the settings type name used as part of the cache key.
    /// </summary>
    public string SettingsType { get; init; }

    /// <summary>
    /// Gets the site ID used as part of the cache key.
    /// </summary>
    public Guid? SiteId { get; init; }

    /// <summary>
    /// Gets the language code used as part of the cache key.
    /// </summary>
    public string? LanguageCode { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsCacheKey"/> struct.
    /// </summary>
    /// <param name="settingsType">The settings type name.</param>
    /// <param name="siteId">The site ID.</param>
    /// <param name="languageCode">The language code.</param>
    /// <exception cref="ArgumentNullException">Thrown when settingsType is null.</exception>
    public SettingsCacheKey(string settingsType, Guid? siteId, string? languageCode)
    {
        SettingsType = settingsType ?? throw new ArgumentNullException(nameof(settingsType));
        SiteId = siteId;
        LanguageCode = languageCode;
    }

    /// <summary>
    /// Creates a cache key for a specific settings type.
    /// </summary>
    /// <typeparam name="T">The settings type.</typeparam>
    /// <param name="siteId">The site ID.</param>
    /// <param name="languageCode">The language code.</param>
    /// <returns>A new cache key instance.</returns>
    public static SettingsCacheKey Create<T>(Guid? siteId, string? languageCode)
    {
        return new SettingsCacheKey(typeof(T).FullName!, siteId, languageCode);
    }

    /// <summary>
    /// Returns a string representation of the cache key.
    /// </summary>
    /// <returns>A string in the format "SettingsType:SiteId:LanguageCode".</returns>
    public override string ToString()
    {
        return $"{SettingsType}:{SiteId?.ToString() ?? "null"}:{LanguageCode ?? "null"}";
    }
}