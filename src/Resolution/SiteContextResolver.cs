using EPiServer.ServiceLocation;
using EPiServer.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Avantibit.Optimizely.CustomSettings.Resolution;

/// <summary>
/// Resolves the current site context from Optimizely CMS using ISiteDefinitionResolver.
/// </summary>
[ServiceConfiguration(typeof(ISiteContextResolver), Lifecycle = ServiceInstanceScope.Singleton)]
public class SiteContextResolver : ISiteContextResolver
{
    private readonly ISiteDefinitionResolver _siteDefinitionResolver;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteDefinitionRepository _siteDefinitionRepository;
    private readonly ILogger<SiteContextResolver> _logger;

    public SiteContextResolver(
        ISiteDefinitionResolver siteDefinitionResolver,
        IHttpContextAccessor httpContextAccessor,
        ISiteDefinitionRepository siteDefinitionRepository,
        ILogger<SiteContextResolver> logger)
    {
        _siteDefinitionResolver = siteDefinitionResolver ?? throw new ArgumentNullException(nameof(siteDefinitionResolver));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _siteDefinitionRepository = siteDefinitionRepository ?? throw new ArgumentNullException(nameof(siteDefinitionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Guid? GetCurrentSiteId()
    {
        try
        {
            var siteDefinition = GetCurrentSiteDefinition();

            if (siteDefinition is not null && siteDefinition.Id != Guid.Empty)
            {
                _logger.LogDebug(
                    "Resolved site ID: {SiteId} (Name: {SiteName})",
                    siteDefinition.Id,
                    siteDefinition.Name);
                return siteDefinition.Id;
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
        {
            return siteId.Value;
        }

        try
        {
            var defaultSite = SiteDefinition.Current;
            if (defaultSite != null && defaultSite.Id != Guid.Empty)
            {
                _logger.LogDebug(
                    "Falling back to default site: {SiteId} (Name: {SiteName})",
                    defaultSite.Id,
                    defaultSite.Name);
                return defaultSite.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error accessing default site definition");
        }

        _logger.LogDebug("No site context available, using empty GUID as fallback");
        return Guid.Empty;
    }

    public string? GetCurrentSiteName()
    {
        try
        {
            var siteDefinition = GetCurrentSiteDefinition();

            if (siteDefinition is not null && !string.IsNullOrWhiteSpace(siteDefinition.Name))
            {
                _logger.LogDebug("Resolved site name: {SiteName}", siteDefinition.Name);
                return siteDefinition.Name;
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
    /// Gets the current site definition using port-based host matching.
    /// </summary>
    /// <returns>The current site definition, or null if not found.</returns>
    private SiteDefinition? GetCurrentSiteDefinition()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            _logger.LogDebug("No HTTP context available for site resolution");
            return null;
        }

        var hostname = httpContext.Request.Host.Host;
        var port = httpContext.Request.Host.Port;
        var requestUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

        var allSites = _siteDefinitionRepository.List();

        foreach (var site in allSites)
        {
            foreach (var siteHost in site.Hosts)
            {
                if (MatchesHost(siteHost, hostname, port, requestUrl))
                {
                    _logger.LogDebug(
                        "Matched site '{SiteName}' (ID: {SiteId}) via host '{HostUrl}'",
                        site.Name,
                        site.Id,
                        siteHost.Url);
                    return site;
                }
            }
        }

        var fallbackSite = _siteDefinitionResolver.GetByHostname(hostname, fallbackToWildcard: true);

        if (fallbackSite is not null)
        {
            _logger.LogDebug(
                "Using fallback site '{SiteName}' (ID: {SiteId})",
                fallbackSite.Name,
                fallbackSite.Id);
            return fallbackSite;
        }

        try
        {
            return SiteDefinition.Current;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a site host definition matches the current request.
    /// </summary>
    /// <param name="siteHost">The site host definition.</param>
    /// <param name="hostname">The request hostname.</param>
    /// <param name="port">The request port.</param>
    /// <param name="requestUrl">The full request URL.</param>
    /// <returns>True if the host matches; otherwise, false.</returns>
    private bool MatchesHost(HostDefinition siteHost, string hostname, int? port, string requestUrl)
    {
        if (siteHost?.Url == null)
            return false;

        var hostUrl = siteHost.Url.ToString().TrimEnd('/');
        var normalizedRequestUrl = requestUrl.TrimEnd('/');

        //Exact match (including port)
        if (hostUrl.Equals(normalizedRequestUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        //Match hostname:port pattern
        if (port.HasValue && hostUrl.Contains($":{port}"))
        {
            return true;
        }

        //Match just the hostname part (if no port specified in site definition)
        if (!hostUrl.Contains(":") && hostUrl.EndsWith(hostname, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}