using EPiServer.Applications;
using EPiServer.ServiceLocation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Avantibit.Optimizely.CustomSettings.Resolution;

/// <summary>
/// Resolves the current site/application context from Optimizely CMS 13 via IApplicationResolver.
/// </summary>
[ServiceConfiguration(typeof(ISiteContextResolver), Lifecycle = ServiceInstanceScope.Singleton)]
public class SiteContextResolver : ISiteContextResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IApplicationResolver _applicationResolver;
    private readonly ILogger<SiteContextResolver> _logger;

    public SiteContextResolver(
        IHttpContextAccessor httpContextAccessor,
        IApplicationResolver applicationResolver,
        ILogger<SiteContextResolver> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _applicationResolver = applicationResolver ?? throw new ArgumentNullException(nameof(applicationResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Guid? GetCurrentSiteId()
    {
        try
        {
            var app = ResolveCurrentApplication();
            if (app != null)
            {
                var id = GenerateApplicationId(app.Name);
                _logger.LogDebug("Resolved application ID: {ApplicationId} (Name: {AppName})", id, app.Name);
                return id;
            }

            _logger.LogWarning("Unable to resolve current site ID from context");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving current site ID");
            return null;
        }
    }

    public Guid GetCurrentSiteIdOrDefault()
    {
        var siteId = GetCurrentSiteId();

        if (siteId.HasValue && siteId.Value != Guid.Empty)
            return siteId.Value;

        _logger.LogDebug("No site context available, using empty GUID as fallback");
        return Guid.Empty;
    }

    public string? GetCurrentSiteName()
    {
        try
        {
            var app = ResolveCurrentApplication();
            if (app != null)
            {
                var name = app.DisplayName ?? app.Name;
                _logger.LogDebug("Resolved application name: {Name}", name);
                return name;
            }

            _logger.LogWarning("Unable to resolve current site name from context");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving current site name");
            return null;
        }
    }

    /// <summary>
    /// Generates a deterministic Guid from an application name using MD5 hash.
    /// Ensures the same application name always maps to the same Guid across requests.
    /// </summary>
    public static Guid GenerateApplicationId(string applicationName)
    {
        var data = MD5.HashData(Encoding.UTF8.GetBytes(applicationName));
        return new Guid(data);
    }

    private Application? ResolveCurrentApplication()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _logger.LogDebug("No HTTP context available for site resolution");
            return null;
        }

        var hostname = httpContext.Request.Host.Host;

        var appByHostname = _applicationResolver.GetByHostname(hostname, fallbackToDefault: true);
        if (appByHostname?.Application != null)
            return appByHostname.Application;

        return _applicationResolver.GetByContext();
    }
}