using Avantibit.Optimizely.CustomSettings.Attributes;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Avantibit.Optimizely.CustomSettings.Discovery;

/// <summary>
/// Implementation of settings discovery service that scans assemblies at startup
/// to find all classes decorated with [SettingsGroup] attribute.
/// </summary>
public class SettingsDiscoveryService : ISettingsDiscoveryService
{
    private static readonly string[] SystemAssemblyPrefixes =
    [
        "System",
        "Microsoft",
        "EPiServer",
        "Optimizely",
        "netstandard",
        "mscorlib",
    ];

    private readonly ILogger<SettingsDiscoveryService> _logger;
    private readonly Lazy<IReadOnlyCollection<SettingsGroupInfo>> _discoveredGroups;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsDiscoveryService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public SettingsDiscoveryService(ILogger<SettingsDiscoveryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _discoveredGroups = new Lazy<IReadOnlyCollection<SettingsGroupInfo>>(DiscoverSettingsGroups);
    }

    /// <summary>
    /// Gets all discovered settings groups, ordered by sort order.
    /// </summary>
    /// <returns>A read-only collection of discovered settings groups.</returns>
    public IReadOnlyCollection<SettingsGroupInfo> GetAllGroups()
    {
        return _discoveredGroups.Value;
    }

    /// <summary>
    /// Gets a specific settings group by its identifier.
    /// </summary>
    /// <param name="id">The settings group identifier.</param>
    /// <returns>The settings group information, or null if not found.</returns>
    public SettingsGroupInfo? GetGroupById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return _discoveredGroups.Value.FirstOrDefault(g =>
            g.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets a specific settings group by its .NET type.
    /// </summary>
    /// <typeparam name="T">The settings type. Must be a reference type.</typeparam>
    /// <returns>The settings group information, or null if not found.</returns>
    public SettingsGroupInfo? GetGroupByType<T>() where T : class
    {
        return _discoveredGroups.Value.FirstOrDefault(g => g.SettingsType == typeof(T));
    }

    /// <summary>
    /// Discovers all settings groups by scanning loaded assemblies.
    /// </summary>
    private IReadOnlyCollection<SettingsGroupInfo> DiscoverSettingsGroups()
    {
        var discoveredGroups = new List<SettingsGroupInfo>();

        try
        {
            var assemblies = GetAssembliesToScan();

            foreach (var assembly in assemblies)
            {
                try
                {
                    DiscoverGroupsInAssembly(assembly, discoveredGroups);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error scanning assembly {AssemblyName} for settings groups",
                        assembly.FullName);
                }
            }

            var sortedGroups = discoveredGroups.OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ToList();

            return sortedGroups.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during settings group discovery");
            return Array.Empty<SettingsGroupInfo>();
        }
    }

    /// <summary>
    /// Gets assemblies that should be scanned for settings groups.
    /// </summary>
    /// <returns>A list of assemblies to scan.</returns>
    private List<Assembly> GetAssembliesToScan()
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !IsSystemAssembly(a))
            .ToList();

        _logger.LogDebug("Scanning {Count} assemblies for settings groups", loadedAssemblies.Count);

        return loadedAssemblies;
    }

    /// <summary>
    /// Checks if an assembly is a system assembly that should be excluded from scanning.
    /// </summary>
    /// <param name="assembly">The assembly to check.</param>
    /// <returns>True if the assembly is a system assembly; otherwise, false.</returns>
    private static bool IsSystemAssembly(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name ?? string.Empty;
        return Array.Exists(SystemAssemblyPrefixes,
            prefix => assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Discovers settings groups in a specific assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for settings groups.</param>
    /// <param name="discoveredGroups">The list to add discovered groups to.</param>
    private void DiscoverGroupsInAssembly(Assembly assembly, List<SettingsGroupInfo> discoveredGroups)
    {
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            //Handles partially loaded assemblies
            types = ex.Types.Where(t => t != null).ToArray()!;
            _logger.LogWarning(
                "Partial type load from assembly {AssemblyName}. Some types could not be loaded. This may indicate invalid or incompatible types.",
                assembly.FullName);
            
            if (ex.LoaderExceptions.Any())
            {
                foreach (var loaderEx in ex.LoaderExceptions.Where(e => e != null).Take(3))
                {
                    _logger.LogDebug("Loader exception: {Message}", loaderEx!.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load types from assembly {AssemblyName}. Skipping this assembly.", assembly.FullName);
            return;
        }

        foreach (var type in types)
        {
            if (!type.IsClass || type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            var attribute = type.GetCustomAttribute<SettingsGroupAttribute>();

            if (attribute is null)
            {
                continue;
            }

            try
            {
                var groupInfo = CreateSettingsGroupInfo(type, attribute);

                if (ValidateSettingsGroup(groupInfo))
                {
                    discoveredGroups.Add(groupInfo);

                    _logger.LogDebug(
                        "Discovered settings group: {Name} ({Type}) with sort order {SortOrder}",
                        groupInfo.Name,
                        type.FullName,
                        groupInfo.SortOrder);
                }
                else
                {
                    _logger.LogWarning(
                        "Settings group from type {TypeName} has invalid definition and will be skipped. Ensure the [SettingsGroup] attribute has required properties.",
                        type.FullName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to process settings group from type {TypeName} in assembly {AssemblyName}. This settings group will be skipped. Check if the class has proper structure and the [SettingsGroup] attribute is correctly applied.",
                    type.FullName,
                    assembly.FullName);
            }
        }
    }

    /// <summary>
    /// Creates a SettingsGroupInfo instance from a type and its attribute.
    /// </summary>
    /// <param name="type">The .NET type of the settings class.</param>
    /// <param name="attribute">The SettingsGroup attribute from the type.</param>
    /// <returns>A new SettingsGroupInfo instance.</returns>
    private static SettingsGroupInfo CreateSettingsGroupInfo(Type type, SettingsGroupAttribute attribute)
    {
        // Use primary constructor for record type
        return new SettingsGroupInfo(
            type,
            attribute.Name,
            type.FullName ?? Guid.NewGuid().ToString(),
            attribute.Description,
            attribute.Icon,
            attribute.AuthorizationPolicy,
            attribute.SortOrder
            );
    }

    /// <summary>
    /// Validates a settings group definition.
    /// </summary>
    /// <param name="groupInfo">The settings group information to validate.</param>
    /// <returns>True if the settings group is valid; otherwise, false.</returns>
    private bool ValidateSettingsGroup(SettingsGroupInfo groupInfo)
    {
        if (string.IsNullOrWhiteSpace(groupInfo.Name))
        {
            _logger.LogWarning(
                "Settings group {Type} has no name defined. Skipping.",
                groupInfo.SettingsType.FullName);
            return false;
        }

        if (string.IsNullOrWhiteSpace(groupInfo.Id))
        {
            _logger.LogWarning(
                "Settings group {Name} has no valid identifier. Skipping.",
                groupInfo.Name);
            return false;
        }

        return true;
    }
}