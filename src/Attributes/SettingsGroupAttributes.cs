namespace Avantibit.Optimizely.CustomSettings.Attributes;

/// <summary>
/// Marks a class as a custom settings group that will be discovered at startup
/// and made available in the Optimizely CMS admin interface.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class SettingsGroupAttribute : Attribute
{
    /// <summary>
    /// Gets the display name of the settings group.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the settings group.
    /// </summary>
    public string? Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort order for the settings group in the menu.
    /// Lower numbers appear first.
    /// </summary>
    public int SortOrder { get; set; } = 100;

    /// <summary>
    /// Gets or sets the authorization policy required to access this settings group.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

}