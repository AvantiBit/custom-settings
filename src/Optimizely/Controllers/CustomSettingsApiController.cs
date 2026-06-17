using Avantibit.Optimizely.CustomSettings.Caching;
using Avantibit.Optimizely.CustomSettings.Configuration;
using Avantibit.Optimizely.CustomSettings.Discovery;
using Avantibit.Optimizely.CustomSettings.Persistence.Abstractions;
using Avantibit.Optimizely.CustomSettings.Resolution;
using Avantibit.Optimizely.CustomSettings.Schema;
using Avantibit.Optimizely.CustomSettings.Validation;
using EPiServer;
using EPiServer.Applications;
using EPiServer.Core;
using EPiServer.Security;
using EPiServer.Web;
using EPiServer.Web.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Avantibit.Optimizely.CustomSettings.Optimizely.Controllers;

[Authorize]
[Route("customsettings/api")]
[ApiController]
public class CustomSettingsApiController : ControllerBase
{
    private readonly ISettingsDiscoveryService _discoveryService;
    private readonly ISettingsSchemaBuilder _schemaBuilder;
    private readonly ISettingsRepository _repository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly ILanguageContextResolver _languageContextResolver;
    private readonly ISettingsCacheService _cacheService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISettingsViewModelFactory _viewModelFactory;
    private readonly IContentLoader _contentLoader;
    private readonly IContentAccessEvaluator _contentAccessEvaluator;
    private readonly IUrlResolver _urlResolver;
    private readonly SystemDefinition _systemDefinition;
    private readonly ILogger<CustomSettingsApiController> _logger;

    public CustomSettingsApiController(
        ISettingsDiscoveryService discoveryService,
        ISettingsSchemaBuilder schemaBuilder,
        ISettingsRepository repository,
        IApplicationRepository applicationRepository,
        ILanguageContextResolver languageContextResolver,
        ISettingsCacheService cacheService,
        IAuthorizationService authorizationService,
        ISettingsViewModelFactory viewModelFactory,
        IContentLoader contentLoader,
        IContentAccessEvaluator contentAccessEvaluator,
        IUrlResolver urlResolver,
        SystemDefinition systemDefinition,
        ILogger<CustomSettingsApiController> logger)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _schemaBuilder = schemaBuilder ?? throw new ArgumentNullException(nameof(schemaBuilder));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _applicationRepository = applicationRepository ?? throw new ArgumentNullException(nameof(applicationRepository));
        _languageContextResolver = languageContextResolver ?? throw new ArgumentNullException(nameof(languageContextResolver));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
        _contentLoader = contentLoader ?? throw new ArgumentNullException(nameof(contentLoader));
        _contentAccessEvaluator = contentAccessEvaluator ?? throw new ArgumentNullException(nameof(contentAccessEvaluator));
        _urlResolver = urlResolver ?? throw new ArgumentNullException(nameof(urlResolver));
        _systemDefinition = systemDefinition ?? throw new ArgumentNullException(nameof(systemDefinition));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("schema/{groupId}")]
    public async Task<IActionResult> GetSchema(string groupId)
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

        var schema = _schemaBuilder.BuildSchema(group.SettingsType);
        return Content(schema.ToJsonString(), "application/json");
    }

    [HttpGet("context")]
    public async Task<IActionResult> GetContext()
    {
        var apps = await _applicationRepository.ListAsync();
        var sites = apps
            .Select(a => new
            {
                id = SiteContextResolver.GenerateApplicationId(a.Name).ToString(),
                name = a.DisplayName ?? a.Name
            })
            .ToList();

        var languages = _languageContextResolver.GetAvailableLanguages()
            .Select(l => new { code = l, name = l.ToUpperInvariant() })
            .ToList();

        return Ok(new { sites, languages });
    }

    [HttpGet("settings/{groupId}")]
    public async Task<IActionResult> GetSettings(string groupId, [FromQuery] string? siteId, [FromQuery] string? language)
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

        Guid? parsedSiteId = null;
        if (!string.IsNullOrEmpty(siteId))
        {
            if (!Guid.TryParse(siteId, out var siteGuid))
                return BadRequest(new { error = "Invalid site ID format." });
            if (!ApplicationExists(siteGuid))
                return BadRequest(new { error = $"Site '{siteId}' does not exist." });
            parsedSiteId = siteGuid;
        }

        if (ValidateLanguage(language, parsedSiteId) is { } langError)
            return langError;

        var entity = await _repository.GetAsync(groupId, parsedSiteId, language);
        var masterLanguage = _languageContextResolver.GetMasterLanguage();
        var fallbackInfo = new Dictionary<string, object>();

        // Parse current-language data when it exists; null means no row for this language yet.
        var data = entity?.JsonData != null ? JsonSerializer.Deserialize<JsonNode>(entity.JsonData) : null;

        // Always query master language for [FallbackToMasterLanguage] properties,
        // even when the current-language row is missing entirely.
        if (!string.Equals(language, masterLanguage, StringComparison.OrdinalIgnoreCase))
        {
            var masterEntity = await _repository.GetAsync(groupId, parsedSiteId, masterLanguage);

            if (masterEntity?.JsonData != null)
            {
                var masterData = JsonSerializer.Deserialize<JsonNode>(masterEntity.JsonData);

                var fallbackProperties = group.SettingsType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<Attributes.FallbackToMasterLanguageAttribute>() != null)
                    .Select(p => p.Name)
                    .ToList();

                foreach (var propName in fallbackProperties)
                {
                    var currentValue = data?[propName];
                    var masterValue = masterData?[propName];

                    if (IsNullOrEmpty(currentValue) && !IsNullOrEmpty(masterValue))
                    {
                        fallbackInfo[propName] = new
                        {
                            isFallback = true,
                            masterValue = masterValue?.ToString(),
                            masterLanguage = masterLanguage
                        };
                    }
                }
            }
        }

        return Content(JsonSerializer.Serialize(new
        {
            values = data != null ? (object)data : new { },
            fallbackInfo = fallbackInfo,
            masterLanguage = masterLanguage,
            currentLanguage = language
        }), "application/json");
    }

    [HttpPost("settings/{groupId}")]
    public async Task<IActionResult> SaveSettings(
        string groupId,
        [FromQuery] string? siteId,
        [FromQuery] string? language)
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

        // Validate query parameters BEFORE reading the request body so that malformed
        // siteId / language return 400 without touching the body stream.
        Guid? parsedSiteId = null;
        if (!string.IsNullOrEmpty(siteId))
        {
            if (!Guid.TryParse(siteId, out var siteGuid))
                return BadRequest(new { error = "Invalid site ID format." });
            if (!ApplicationExists(siteGuid))
                return BadRequest(new { error = $"Site '{siteId}' does not exist." });
            parsedSiteId = siteGuid;
        }

        if (ValidateLanguage(language, parsedSiteId) is { } langError)
            return langError;

        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();

        object viewModel;
        try
        {
            // Parse inside the try-catch: malformed JSON (e.g. "{") throws JsonException here.
            var data = string.IsNullOrEmpty(rawBody) ? null : JsonNode.Parse(rawBody);
            viewModel = _viewModelFactory.CreateViewModel(group.SettingsType, data);
        }
        catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is FormatException)
        {
            _logger.LogWarning(ex,
                "Failed to deserialize settings for GroupId={GroupId}: {Message}",
                groupId, ex.Message);
            return BadRequest(new
            {
                errors = new Dictionary<string, List<string>>
                {
                    ["_general"] = new List<string> { "One or more values have an invalid format. Please check your input and try again." }
                }
            });
        }

        if (!_viewModelFactory.ValidateViewModel(viewModel, out var validationErrors))
        {
            _logger.LogWarning(
                "Validation failed for GroupId={GroupId}: {Errors}",
                groupId,
                JsonSerializer.Serialize(validationErrors));

            return BadRequest(new { errors = validationErrors });
        }

        var validatedJson = _viewModelFactory.MapToJson(viewModel).ToJsonString();

        var entity = new SettingsEntity
        {
            SettingsType = groupId,
            SiteId = parsedSiteId,
            LanguageCode = language,
            JsonData = validatedJson,
            CreatedUtc = DateTime.UtcNow,
            ModifiedUtc = DateTime.UtcNow,
            Version = 1
        };

        try
        {
            await _repository.SaveAsync(entity);
            await _cacheService.LoadAllAsync();

            _logger.LogInformation(
                "Settings saved and cache reloaded: GroupId={GroupId}, SiteId={SiteId}, Language={Language}",
                groupId,
                parsedSiteId,
                language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to save settings: GroupId={GroupId}, SiteId={SiteId}, Language={Language}",
                groupId,
                siteId,
                language);
            return StatusCode(500, new { error = "Failed to save settings" });
        }

        return Ok();
    }

    /// <summary>
    /// Gets available languages for a specific site.
    /// </summary>
    /// <param name="siteId">The site ID.</param>
    /// <returns>The available languages for the site, or BadRequest if the site ID is invalid.</returns>
    [HttpGet("context/languages/{siteId}")]
    public IActionResult GetLanguagesForSite(string siteId)
    {
        if (!Guid.TryParse(siteId, out var parsedSiteId))
        {
            return BadRequest(new { error = "Invalid site ID" });
        }

        var languages = _languageContextResolver.GetAvailableLanguagesForSite(parsedSiteId)
            .Select(l => new { code = l, name = l.ToUpperInvariant() })
            .ToList();

        _logger.LogInformation("Returning {Count} languages for site {SiteId}: {Languages}",
            languages.Count,
            siteId,
            string.Join(", ", languages.Select(l => l.code)));

        return Ok(new { languages });
    }

    /// <summary>
    /// Searches for CMS content matching a query string.
    /// Optional <paramref name="type"/> filter: "page" | "media" | "block" | "all" (default).
    /// When no query is provided, ContentFolder items are included so the tree can show folder hierarchy.
    /// </summary>
    [HttpGet("content/search")]
    [Authorize(Policy = CustomSettingsConstants.DefaultPolicyName)]
    public IActionResult SearchContent([FromQuery] string? q, [FromQuery] string? language, [FromQuery] string? type)
    {
        try
        {
            var lang = language ?? _languageContextResolver.GetCurrentLanguageOrDefault() ?? "en";
            bool hasQuery = !string.IsNullOrWhiteSpace(q);

            // Collect content roots based on the requested type
            var roots = new List<ContentReference>();

            bool wantPages  = type is null or "all" or "page";
            bool wantMedia  = type is null or "all" or "media";
            bool wantBlocks = type is null or "all" or "block";

            if (wantPages)
                roots.Add(ContentReference.RootPage);

            if (wantBlocks && !ContentReference.IsNullOrEmpty(ContentReference.GlobalBlockFolder))
                roots.Add(ContentReference.GlobalBlockFolder);

            if (wantMedia)
            {
                // Global assets root (system-wide, replaces SiteDefinition.GlobalAssetsRoot)
                var globalAssetsRoot = _systemDefinition.GlobalAssetsRoot;
                if (!ContentReference.IsNullOrEmpty(globalAssetsRoot))
                    roots.Add(globalAssetsRoot);

                // Per-application assets roots (replaces SiteDefinition.SiteAssetsRoot)
                foreach (var app in _applicationRepository.List().OfType<IResourceableApplication>())
                {
                    if (!ContentReference.IsNullOrEmpty(app.AssetsRoot) &&
                        app.AssetsRoot != globalAssetsRoot)
                        roots.Add(app.AssetsRoot!);
                }
            }

            var content = roots
                .Distinct()
                .SelectMany(r =>
                {
                    try { return _contentLoader.GetDescendents(r); }
                    catch { return Array.Empty<ContentReference>(); }
                })
                .Distinct()
                .Select(r =>
                {
                    try { return _contentLoader.Get<IContent>(r, new LanguageSelector(lang)); }
                    catch { return null; }
                })
                // Keep real folders (for tree hierarchy), plus typed content.
                // ContentAssetFolder is the per-page asset container auto-created by Optimizely
                // for each page — exclude those; keep only real ContentFolder (global/site asset roots).
                .Where(c => (c is ContentFolder && c is not ContentAssetFolder) || c is PageData || c is MediaData || c is BlockData)
                // Published check (ContentFolder is not IVersionable → always passes)
                .Where(c => c is not IVersionable iv || iv.Status == VersionStatus.Published)
                // Access check — filter out content the current user cannot read
                .Where(c => _contentAccessEvaluator.HasAccess(c!, User, AccessLevel.Read))
                // Type filter — when searching (flat), exclude folders
                .Where(c =>
                {
                    if (c is ContentFolder) return !hasQuery; // folders only in tree (no-query) mode
                    if (c is PageData)  return wantPages;
                    if (c is MediaData) return wantMedia;
                    if (c is BlockData) return wantBlocks;
                    return false;
                })
                // Name search — only applied to non-folder items
                .Where(c => !hasQuery || c!.Name.Contains(q!, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c is ContentFolder ? 0 : 1)   // folders first within their level
                .ThenBy(c => c!.Name)
                .Take(500)
                .Select(c =>
                {
                    bool isFolder = c is ContentFolder;
                    string ct = c is PageData ? "page" : c is MediaData ? "media" : c is BlockData ? "block" : "folder";
                    string url  = isFolder ? string.Empty : (_urlResolver.GetUrl(c!.ContentLink, lang) ?? string.Empty);
                    return new
                    {
                        id           = c!.ContentLink.ID,
                        workId       = c.ContentLink.WorkID,
                        providerName = c.ContentLink.ProviderName,
                        parentId     = c.ParentLink?.ID ?? 0,
                        name         = c.Name,
                        contentType  = ct,
                        selectable   = !isFolder,
                        url
                    };
                })
                .ToList();

            return Ok(new { pages = content });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Content search failed for query: {Query}", q);
            return Ok(new { pages = Array.Empty<object>() });
        }
    }

    /// <summary>
    /// Returns name, URL and content type for a single content item by its integer ID.
    /// Used by the ContentReference widget to restore the display after page reload.
    /// </summary>
    [HttpGet("content/{id:int}")]
    [Authorize(Policy = CustomSettingsConstants.DefaultPolicyName)]
    public IActionResult GetContentById(int id, [FromQuery] string? language)
    {
        try
        {
            var lang = language ?? _languageContextResolver.GetCurrentLanguageOrDefault() ?? "en";
            var content = _contentLoader.Get<IContent>(new ContentReference(id), new LanguageSelector(lang));

            if (!_contentAccessEvaluator.HasAccess(content, User, AccessLevel.Read))
            {
                return NotFound();
            }

            return Ok(new
            {
                id          = content.ContentLink.ID,
                name        = content.Name,
                contentType = content is PageData ? "page" : content is MediaData ? "media" : "block",
                url         = _urlResolver.GetUrl(content.ContentLink, lang) ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetContentById failed for id: {Id}", id);
            return NotFound();
        }
    }

    private async Task<bool> IsAuthorizedForGroupAsync(SettingsGroupInfo group)
    {
        var policy = string.IsNullOrEmpty(group.AuthorizationPolicy)
            ? CustomSettingsConstants.DefaultPolicyName
            : group.AuthorizationPolicy;

        var result = await _authorizationService.AuthorizeAsync(User, policy);
        return result.Succeeded;
    }

    private bool ApplicationExists(Guid applicationId)
    {
        var applications = _applicationRepository.List().ToList();
        if (applications.Count == 0)
        {
            return true;
        }

        return applications.Any(a => SiteContextResolver.GenerateApplicationId(a.Name) == applicationId);
    }

    /// <summary>
    /// Validates the language code against available languages for the given site.
    /// Returns a <see cref="BadRequestObjectResult"/> when the language is invalid, or null when valid.
    /// </summary>
    private IActionResult? ValidateLanguage(string? language, Guid? siteId)
    {
        if (string.IsNullOrEmpty(language))
            return null;

        // Guard against strings that exceed the database column length before hitting the DB.
        if (language.Length > 10)
            return BadRequest(new { error = $"Language code '{language}' is too long (max 10 characters)." });

        var available = siteId.HasValue
            ? _languageContextResolver.GetAvailableLanguagesForSite(siteId.Value)
            : _languageContextResolver.GetAvailableLanguages();

        var availableList = available.ToList();

        if (!availableList.Contains(language, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = $"Language '{language}' is not available for the requested site." });
        }

        return null;
    }

    private static bool IsNullOrEmpty(JsonNode? node)
    {
        if (node == null)
        {
            return true;
        }

        var value = node.ToString();
        return string.IsNullOrWhiteSpace(value) || value == "null";
    }
}
