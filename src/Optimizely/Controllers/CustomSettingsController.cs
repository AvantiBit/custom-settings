using Avantibit.Optimizely.CustomSettings.Configuration;
using Avantibit.Optimizely.CustomSettings.Discovery;
using Avantibit.Optimizely.CustomSettings.Optimizely.Models;
using Avantibit.Optimizely.CustomSettings.Resolution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Avantibit.Optimizely.CustomSettings.Optimizely.Controllers;

/// <summary>
/// Controller for rendering custom settings UI views.
/// </summary>
[Authorize]
[Route("/EPiServerPlugins/CustomSettings")]
public class CustomSettingsController : Controller
{
    private readonly ISettingsDiscoveryService _discoveryService;
    private readonly ISiteContextResolver _siteContextResolver;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<CustomSettingsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomSettingsController"/> class.
    /// </summary>
    /// <param name="discoveryService">The settings discovery service.</param>
    /// <param name="siteContextResolver">The site context resolver.</param>
    public CustomSettingsController(
        ISettingsDiscoveryService discoveryService,
        ISiteContextResolver siteContextResolver,
        IAuthorizationService authorizationService,
        ILogger<CustomSettingsController> logger)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _siteContextResolver = siteContextResolver ?? throw new ArgumentNullException(nameof(siteContextResolver));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Displays the index view listing all available settings groups.
    /// </summary>
    /// <returns>The index view.</returns>
    [HttpGet("/EPiServerPlugins/CustomSettings")]
    public async Task<IActionResult> Index()
    {
        var allGroups = _discoveryService.GetAllGroups();

        var visibleGroups = new List<SettingsGroupInfo>();

        foreach (var group in allGroups)
        {
            if (await IsAuthorizedForGroupAsync(group))
            {
                visibleGroups.Add(group);
            }
        }

        return View(visibleGroups.AsReadOnly());
    }

    /// <summary>
    /// Displays the edit view for a specific settings group.
    /// </summary>
    /// <param name="groupId">The settings group identifier.</param>
    /// <returns>The edit view, or NotFound if the group doesn't exist.</returns>
    [HttpGet("/customsettings/edit/{groupId}")]
    public async Task<IActionResult> Edit(string groupId)
    {
        var group = _discoveryService.GetGroupById(groupId);
        if (group == null)
        {
            return NotFound();
        }

        if (!await IsAuthorizedForGroupAsync(group))
        {
            return Forbid();
        }

        var currentSiteId = _siteContextResolver.GetCurrentSiteIdOrDefault();

        var viewModel = new EditSettingsViewModel
        {
            GroupId = groupId,
            GroupName = group.Name,
            GroupDescription = group.Description ?? string.Empty,
            CurrentSiteId = currentSiteId.ToString()
        };

        return View(viewModel);
    }

    private async Task<bool> IsAuthorizedForGroupAsync(SettingsGroupInfo group)
    {
        var policy = string.IsNullOrEmpty(group.AuthorizationPolicy)
            ? CustomSettingsConstants.DefaultPolicyName
            : group.AuthorizationPolicy;

        var result = await _authorizationService.AuthorizeAsync(User, policy);
        return result.Succeeded;
    }
}
