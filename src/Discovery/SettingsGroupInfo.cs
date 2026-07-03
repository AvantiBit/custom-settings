namespace Avantibit.Optimizely.CustomSettings.Discovery;

/// <summary>
/// Represents metadata about a discovered settings group.
/// </summary>
/// <param name="SettingsType">The .NET type of the settings class.</param>
/// <param name="Name">The display name of the settings group.</param>
/// <param name="Id">The unique identifier of the settings group.</param>
/// <param name="Description">The description of the settings group.</param>
/// <param name="AuthorizationPolicy">The authorization policy required to access the settings group.</param>
/// <param name="SortOrder">The sort order for the settings group in the menu. Default is 0.</param>
public record SettingsGroupInfo(
    Type SettingsType,
    string Name,
    string Id,
    string? Description,
    string? AuthorizationPolicy,
    int SortOrder = 0);