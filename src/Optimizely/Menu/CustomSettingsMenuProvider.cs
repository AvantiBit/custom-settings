using Avantibit.Optimizely.CustomSettings.Configuration;
using Avantibit.Optimizely.CustomSettings.Discovery;
using EPiServer.Shell.Navigation;

namespace Avantibit.Optimizely.CustomSettings.Optimizely.Menu;

/// <summary>
/// Menu provider that dynamically creates menu items for discovered settings groups.
/// </summary>
[MenuProvider]
public class CustomSettingsMenuProvider : IMenuProvider
{
    private readonly ISettingsDiscoveryService _discoveryService;

    private const string ParentMenuPath = MenuPaths.Global + "/cms/adminplugin";
    private const int ParentSortIndex = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomSettingsMenuProvider"/> class.
    /// </summary>
    /// <param name="discoveryService">The settings discovery service.</param>
    public CustomSettingsMenuProvider(
        ISettingsDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
    }

    /// <summary>
    /// Gets all menu items for custom settings groups.
    /// </summary>
    /// <returns>A collection of menu items.</returns>
    public IEnumerable<MenuItem> GetMenuItems()
    {
        var menuItems = new List<MenuItem>();

        var parentMenuItem = new UrlMenuItem("Custom Settings", ParentMenuPath, "/EPiServerPlugins/CustomSettings")
        {
            SortIndex = ParentSortIndex,
            IsAvailable = _ => true
        };

        menuItems.Add(parentMenuItem);

        var settingsGroups = _discoveryService.GetAllGroups();

        foreach (var group in settingsGroups)
        {
            var subMenuItem = new UrlMenuItem(
                group.Name,
                $"{ParentMenuPath}/{SanitizeMenuPath(group.Id)}",
                $"/customsettings/edit/{group.Id}")
            {
                SortIndex = group.SortOrder,
                AuthorizationPolicy = string.IsNullOrEmpty(group.AuthorizationPolicy)
                    ? CustomSettingsConstants.DefaultPolicyName
                    : group.AuthorizationPolicy
            };

            menuItems.Add(subMenuItem);
        }

        return menuItems;
    }

    /// <summary>
    /// Sanitizes group ID for use in menu path.
    /// </summary>
    /// <param name="id">The group ID to sanitize.</param>
    /// <returns>A sanitized string safe for use in menu paths.</returns>
    private static string SanitizeMenuPath(string id)
    {
        return id.ToLowerInvariant()
            .Replace(".", "_")
            .Replace(" ", "_");
    }
}