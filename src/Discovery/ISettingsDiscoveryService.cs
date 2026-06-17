namespace Avantibit.Optimizely.CustomSettings.Discovery;

/// <summary>
/// Service for discovering and managing custom settings groups defined in the application.
/// </summary>
public interface ISettingsDiscoveryService
{
    /// <summary>
    /// Gets all discovered settings groups, ordered by sort order.
    /// </summary>
    /// <returns>A read-only collection of discovered settings groups.</returns>
    IReadOnlyCollection<SettingsGroupInfo> GetAllGroups();

    /// <summary>
    /// Gets a specific settings group by its identifier.
    /// </summary>
    /// <param name="id">The settings group identifier.</param>
    /// <returns>The settings group information, or null if not found.</returns>
    SettingsGroupInfo? GetGroupById(string id);

    /// <summary>
    /// Gets a specific settings group by its .NET type.
    /// </summary>
    /// <typeparam name="T">The settings type. Must be a reference type.</typeparam>
    /// <returns>The settings group information, or null if not found.</returns>
    SettingsGroupInfo? GetGroupByType<T>() where T : class;
}