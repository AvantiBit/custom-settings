# Avantibit.Optimizely.CustomSettings

A library for managing custom settings in Optimizely CMS 12 applications. This library provides a type-safe way to manage site and language-specific configuration values with built-in caching, automatic discovery, and an admin interface.


| NuGet version | Optimizely CMS version | Branch | Sample |
|---|---|---|---|
| 2.x | CMS 13 | main | samples/AvantiBit.Optimizely.CustomSettings.Sample.Cms13 |
| 1.x | CMS 12 | support/cms12 | samples/CustomSettings.Sample.Cms12 |

## Features

- **Type-safe settings management** - Define strongly-typed settings classes
- **Multi-site and multi-language support** - Manage settings per site and language
- **Automatic discovery** - Settings groups are automatically discovered at startup
- **Built-in caching** - Optimized read performance with intelligent cache invalidation
- **Admin UI integration** - Manage settings directly from the Optimizely CMS admin interface
- **Fallback support** - Configure fallback to master language for specific properties
- **Entity Framework Core** - Persistent storage using SQL Server
- **JSON Schema generation** - Automatic schema generation for validation

## Installation

### Prerequisites

- .NET 10
- Optimizely CMS 12

### NuGet Package

```bash
dotnet add package Avantibit.Optimizely.CustomSettings
```

### Database Setup

The library uses Entity Framework Core migrations. The database schema will be automatically created when you run your application for the first time.

## Configuration

### 1. Add Connection String

In your `appsettings.json`, ensure you have a connection string named `EPiServerDB`:

```json
{
  "ConnectionStrings": {
    "EPiServerDB": "Server=(localdb)\\mssqllocaldb;Database=YourDatabase;Trusted_Connection=True;"
  }
}
```

### 2. Register Services

In your `Startup.cs`, add the custom settings services:

```csharp
using Avantibit.Optimizely.CustomSettings.Extensions;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddCmsAspNetIdentity<ApplicationUser>()
            .AddCms()
            .AddCustomSettings(_configuration) // Add this line
            .AddAlloy();
    }
}
```

### 3. Register endpoints

In your `Startup.cs`, ensure you have the necessary endpoints configured:
```csharp

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapContent();
});
```

### Configuration Options

The `AddCustomSettings` method accepts an `IConfiguration` parameter and automatically:
- Configures the database context with SQL Server
- Registers all necessary services (repository, cache, discovery)
- Sets up the admin UI controllers and views
- Enables automatic settings discovery at startup

## Supported Property Types

The following C# types are supported for settings class properties:

| C# Type | JSON Schema | Nullable | Notes |
|---|---|---|---|
| `string` | `type: string` | ✅ `string?` | Default text input |
| `int` | `type: integer` | ✅ `int?` | Number input with optional `[Range]` |
| `bool` | `type: boolean` | — | Checkbox |
| `DateTime` | `type: string, format: date-time` | ✅ `DateTime?` | Date/time picker |
| `DateTimeOffset` | `type: string, format: date-time` | ✅ `DateTimeOffset?` | Date/time picker |
| `Guid` | `type: string, format: uuid` | ✅ `Guid?` | Text input |
| `Uri` | `type: string, format: uri` | ✅ `Uri?` | URL input with `https://` placeholder |
| `EPiServer.Url` | `type: string, format: url-picker` | ✅ `Url?` | Optimizely URL picker (pages, media, external links) |
| `ContentReference` | `type: object, format: page-reference` | ✅ `ContentReference?` | Optimizely content picker; empty reference is stored as null |
| `IList<string>` | `type: array, items: string` | — | Multi-value list with add/remove |
| `List<string>` | `type: array, items: string` | — | Multi-value list with add/remove |
| `string` + `[Url]` | `type: string, format: uri` | ✅ `string?` | URL input (string with validation) |

### Usage Examples

```csharp
[SettingsGroup(Name = "Integration Settings")]
public class IntegrationSettings
{
    // Basic types
    [Display(Name = "GTM Container ID")]
    public string? GtmContainerId { get; set; }

    [Display(Name = "ERP Sync Batch Size")]
    [Range(1, 5000)]
    public int ErpSyncBatchSize { get; set; } = 500;

    [Display(Name = "Enable Product Feed")]
    public bool EnableProductFeed { get; set; }

    // DateTime
    [Display(Name = "Maintenance Window Start (UTC)")]
    public DateTime? MaintenanceWindowStart { get; set; }

    // Guid
    [Display(Name = "ODP Tracker ID")]
    public Guid? OdpTrackerId { get; set; }

    // URL — using Uri type
    [Display(Name = "ERP API Base URL")]
    public Uri? ErpApiBaseUrl { get; set; }

    // URL — using string with [Url] attribute
    [Url]
    [Display(Name = "Service Status Page")]
    public string? ServiceStatusPageUrl { get; set; }

    // Multi-value string list
    [Display(Name = "Trusted Redirect Hosts")]
    public IList<string> TrustedRedirectHosts { get; set; } = new List<string>();
}
```

For `EPiServer.Url` and `ContentReference` examples, see the `CommerceSettings` class under [Usage](#usage).

> **Keep secrets out of settings.** Values managed here are editable in the admin UI and stored in the database. Identifiers and endpoints (container IDs, base URLs) are a good fit; API keys, connection strings, and other secrets belong in your configuration providers (environment variables, user secrets, key vault).

### Notes

- All value types (`int`, `bool`, `DateTime`, `DateTimeOffset`, `Guid`) are automatically treated as **required** unless declared as nullable (`int?`, `DateTime?`, etc.)
- `IList<string>` and `List<string>` are interchangeable — both produce the same array UI
- `Uri` and `string + [Url]` both produce a URL input field with `https://` placeholder; use `Uri` when you want a typed property, `string` when you need plain string access
- `EPiServer.Url` opens the Optimizely URL picker (link to pages, media, or external URLs), while `ContentReference` opens the Optimizely content picker for selecting a content item directly
- Properties with unsupported types are **skipped with a warning** in the logs — they will not appear in the admin form

## Usage

### 1. Define a Settings Class

Create a class decorated with the `[SettingsGroup]` attribute:

```csharp
using Avantibit.Optimizely.CustomSettings.Attributes;
using EPiServer;
using EPiServer.Core;
using System.ComponentModel.DataAnnotations;

namespace YourProject.Settings;

[SettingsGroup(
    Name = "Commerce Settings",
    Description = "Store-wide commerce behavior",
    SortOrder = 100)]
public class CommerceSettings
{
    [Display(Name = "Default Currency")]
    [Required]
    public string DefaultCurrency { get; set; } = "EUR";

    [Display(Name = "Free Shipping Threshold")]
    [Range(0, 10000)]
    public int FreeShippingThreshold { get; set; } = 50;

    [Display(Name = "Low Stock Warning Threshold")]
    [Range(0, 1000)]
    public int LowStockThreshold { get; set; } = 5;

    [Display(Name = "Enable Click & Collect")]
    public bool EnableClickAndCollect { get; set; }

    // Per-market override; falls back to the master-language value when empty
    [Display(Name = "Customer Service Email")]
    [EmailAddress]
    [FallbackToMasterLanguage]
    public string? CustomerServiceEmail { get; set; }

    [Display(Name = "Order Notification Recipients")]
    public IList<string> OrderNotificationRecipients { get; set; } = new List<string>();

    // Optimizely content picker
    [Display(Name = "Terms & Conditions Page")]
    public ContentReference? TermsPage { get; set; }

    // Optimizely URL picker (page, media, or external URL)
    [Display(Name = "Returns Portal")]
    public Url? ReturnsPortal { get; set; }
}
```

### 2. Inject and Use Settings

In your controllers, views, or services, inject `ICustomSettingsService<T>`:

```csharp
using Avantibit.Optimizely.CustomSettings.Configuration;

public class CheckoutController : Controller
{
    private readonly ICustomSettingsService<CommerceSettings> _settingsService;

    public CheckoutController(ICustomSettingsService<CommerceSettings> settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<IActionResult> Index()
    {
        // Get settings for current site and language
        var settings = await _settingsService.GetAsync();

        ViewBag.Currency = settings.DefaultCurrency;
        ViewBag.FreeShippingThreshold = settings.FreeShippingThreshold;
        ViewBag.CustomerServiceEmail = settings.CustomerServiceEmail;

        return View();
    }
}
```

### 3. Save Settings Programmatically

```csharp
public async Task EnableClickAndCollectAsync()
{
    var settings = await _settingsService.GetAsync();
    settings.EnableClickAndCollect = true;
    settings.LowStockThreshold = 10;

    await _settingsService.SaveAsync(settings);
}
```

### 4. Delete Settings

```csharp
public async Task RemoveSettings()
{
    // Delete settings for current site and language
    var deleted = await _settingsService.DeleteAsync();
    
    if (deleted)
    {
        // Settings were successfully deleted
    }
}
```

### 5. Work with Specific Site/Language

```csharp
// Get settings for a specific site and language
var siteId = Guid.Parse("your-site-id");
var settings = await _settingsService.GetAsync(siteId, "sv");

// Save settings for a specific site and language
await _settingsService.SaveAsync(settings, siteId, "en");

// Delete settings for a specific site and language
await _settingsService.DeleteAsync(siteId, "fr");
```

### 6. Restrict Access with Authorization Policy

You can restrict visibility of a settings group in the admin menu to specific roles using the `AuthorizationPolicy` property.

#### Step 1 — Register a named policy in `Startup.cs`

The value must match a named ASP.NET Core authorization policy — a role name alone is not sufficient.

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminsOnly", policy =>
        policy.RequireRole("CmsAdmins"));
});
```

You can map any combination of roles to a single policy name:

```csharp
options.AddPolicy("EditorsAndAdmins", policy =>
    policy.RequireRole("CmsAdmins", "WebAdmins", "CmsEditors"));
```

#### Step 2 — Apply the policy to your settings class

Integration configuration is a natural candidate — editors rarely need to see it.

```csharp
[SettingsGroup(
    Name = "Integration Settings",
    Description = "Third-party service configuration",
    SortOrder = 200,
    AuthorizationPolicy = "AdminsOnly")]  // must match a registered policy name
public class IntegrationSettings
{
    [Display(Name = "ERP API Base URL")]
    public Uri? ErpApiBaseUrl { get; set; }
}
```

Optimizely Shell will automatically hide this menu entry from users who do not satisfy the policy. Users who do satisfy it will see and access the entry normally.

#### Summary

| Step | Location | What to set |
|------|----------|-------------|
| Register policy | `Startup.cs` → `AddAuthorization` | `options.AddPolicy("PolicyName", ...)` |
| Apply to settings | Settings class attribute | `AuthorizationPolicy = "PolicyName"` |

## API Reference

### Attributes

#### `[SettingsGroup]`
Marks a class as a custom settings group.

**Properties:**
- `Name` (string, required) - Display name in the admin interface
- `Description` (string, optional) - Description shown in the admin interface
- `SortOrder` (int, optional) - Order in the menu (default: 100)
- `AuthorizationPolicy` (string, optional) - Authorization policy name

#### `[FallbackToMasterLanguage]`
Marks a property to fall back to the master/default language value when the current language has no value (null, an empty/whitespace string, or a default value-type value).

**Usage:**
```csharp
[FallbackToMasterLanguage]
public string? CustomerServiceEmail { get; set; }
```

### Interfaces

#### `ICustomSettingsService<T>`
Generic service interface for typed access to custom settings.

**Methods:**

```csharp
// Retrieve settings
Task<T> GetAsync(
    Guid? siteId = null, 
    string? languageCode = null, 
    CancellationToken cancellationToken = default);

// Save settings
Task SaveAsync(
    T settings, 
    Guid? siteId = null, 
    string? languageCode = null, 
    CancellationToken cancellationToken = default);

// Delete settings
Task<bool> DeleteAsync(
    Guid? siteId = null, 
    string? languageCode = null, 
    CancellationToken cancellationToken = default);
```

**Parameters:**
- `siteId` - Optional site identifier (null = current site)
- `languageCode` - Optional language code (null = current language)
- `cancellationToken` - Cancellation token for async operations

#### `ISettingsCacheService`
Pre-populated in-memory settings cache, synchronized across servers by a polling service.

```csharp
// Get a defensive copy of cached settings (returns a default instance if not found)
T Get<T>(Guid? siteId, string? languageCode) where T : class, new();

// Reload all settings from the database, atomically swapping the cache contents
Task LoadAllAsync(CancellationToken cancellationToken = default);

// Cache statistics for monitoring (entries, hits, misses, hit ratio)
CacheStatistics GetStatistics();
```

#### `ISettingsRepository`
Repository interface for data access.

```csharp
Task<SettingsEntity?> GetAsync(string settingsType, Guid? siteId, string? languageCode, CancellationToken cancellationToken = default);
Task SaveAsync(SettingsEntity entity, CancellationToken cancellationToken = default);
Task<bool> DeleteAsync(string settingsType, Guid? siteId, string? languageCode, CancellationToken cancellationToken = default);
Task<List<SettingsEntity>> GetAllAsync(CancellationToken cancellationToken = default);
Task<long> GetVersionAsync(CancellationToken cancellationToken = default);
Task IncrementVersionAsync(CancellationToken cancellationToken = default);
```

### Service Registration

#### `AddCustomSettings(IConfiguration configuration)`
Extension method to register all custom settings services.

**Registered Services:**
- `CustomSettingsDbContext` - Database context (scoped)
- `ISettingsRepository` / `SettingsRepository` - Data access (scoped)
- `ISettingsCacheService` / `SettingsCacheService` - Caching (singleton)
- `ICustomSettingsService<T>` / `CustomSettingsService<T>` - Generic settings service (scoped)
- `ISettingsDiscoveryService` / `SettingsDiscoveryService` - Discovery (singleton)
- `ISettingsSchemaBuilder` / `SettingsSchemaBuilder` - Schema generation (singleton)
- `ISettingsViewModelFactory` / `SettingsViewModelFactory` - Admin form view models (singleton)
- `CustomSettingsMigrationHostedService` - Applies EF Core migrations at startup (hosted service)
- `SettingsDiscoveryHostedService` - Startup discovery (hosted service)
- `SettingsCachePollingService` - Cross-server cache synchronization (hosted service)

It also registers the default authorization policy (WebAdmins/WebEditors/CmsAdmins/CmsEditors), adds the library as an MVC application part, and configures admin view discovery. The context resolvers (`ISiteContextResolver`, `ILanguageContextResolver`, `ISettingsFallbackResolver`) are registered through Optimizely's `[ServiceConfiguration]` attribute.

#### `AddCustomSettings(IConfiguration configuration, Action<SettingsCacheOptions> configureCacheOptions)`
Overload to customize cross-server cache synchronization:

```csharp
services.AddCustomSettings(_configuration, options =>
{
    options.PollingIntervalSeconds = 30; // default: 10
    options.MaxJitterSeconds = 5;        // default: 2
});
```

The polling service watches a version counter in the database and reloads the cache when settings are changed on another server.

## Admin Interface

Once configured, the custom settings management interface is available in the Optimizely CMS admin menu:

1. Log in to your Optimizely CMS admin interface
2. Navigate to **Custom Settings** in the CMS admin menu (each settings group appears as a sub-item)
3. Select the settings group you want to manage
4. Choose the site and language
5. Edit the settings using the generated form
6. Click **Save** to persist changes

The admin interface provides:
- Dropdown selection for site and language
- Auto-generated forms based on your settings class
- Real-time validation based on data annotations

## Troubleshooting Guide

### Common Issues and Solutions

#### Issue: "Connection string 'EPiServerDB' not found"

**Problem:** The required connection string is missing from configuration.

**Solution:**
1. Open `appsettings.json`
2. Add the connection string:
```json
{
  "ConnectionStrings": {
    "EPiServerDB": "Server=(localdb)\\mssqllocaldb;Database=YourDb;Trusted_Connection=True;"
  }
}
```

---

#### Issue: "Failed to configure database context"

**Problem:** The database server is not accessible or the connection string is invalid.

**Solution:**
1. Verify SQL Server is running
2. Test the connection string using SQL Server Management Studio
3. Check firewall settings
4. For LocalDB, ensure SQL Server Express LocalDB is installed

---

#### Issue: Settings not appearing in admin menu

**Problem:** Settings groups are not discovered.

**Solution:**
1. Ensure your settings class has the `[SettingsGroup]` attribute
2. Verify the class is public
3. Check that the assembly containing the settings is referenced by your web project
4. Restart the application to trigger discovery

---

#### Issue: Settings not persisting

**Problem:** Changes are not saved to the database.

**Solution:**
1. Check database permissions for the connection string user
2. Verify the migrations have been applied:
```bash
dotnet ef database update --project YourProject
```
3. Check application logs for Entity Framework errors
4. Ensure `SaveAsync` is being awaited properly

---

#### Issue: Cache not invalidating

**Problem:** Old values are returned after saving.

**Solution:**
1. The cache automatically reloads on save; other servers pick up changes via the polling service (default: every 10 seconds)
2. If issues persist, inject `ISettingsCacheService` and call `LoadAllAsync()` to force a full reload
3. Use `GetStatistics()` to inspect cache entries and hit ratio

---

#### Issue: Language fallback not working

**Problem:** Properties with `[FallbackToMasterLanguage]` don't fallback.

**Solution:**
1. Ensure the master/default language is properly configured in Optimizely
2. Verify that the fallback attribute is applied to the property
3. The property must be null, an empty/whitespace string, or a default value-type value for fallback to occur

---

#### Issue: "Unable to find views"

**Problem:** Razor views for the admin interface are not found.

**Solution:**
1. Ensure `AddCustomSettings()` is called after `AddCms()`
2. Verify the package is properly installed with all dependencies
3. Check that embedded resources are properly configured

---

#### Issue: Migration errors

**Problem:** Database migration fails.

**Solution:**
1. Ensure the database user has DDL permissions
2. Check for existing tables with the same name
3. Run migrations manually:
```bash
dotnet ef migrations add InitialCreate --project YourProject
dotnet ef database update --project YourProject
```

---

### Enable Detailed Logging

To troubleshoot issues, enable detailed logging in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Information",
      "Avantibit.Optimizely.CustomSettings": "Debug"
    }
  }
}
```

---

### Performance Considerations

1. **Caching:** Settings are cached by default. Cache keys include site, language, and settings type.
2. **Discovery:** Settings discovery runs once at startup. Performance impact is minimal.
3. **Database queries:** Indexed by SettingsType, SiteId, and LanguageCode for optimal performance.