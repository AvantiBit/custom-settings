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
[SettingsGroup(Name = "Site Settings")]
public class SiteSettings
{
    // Basic types
    [Display(Name = "Site Name")]
    public string SiteName { get; set; } = "My Site";

    [Display(Name = "Items Per Page")]
    [Range(1, 100)]
    public int ItemsPerPage { get; set; } = 10;

    [Display(Name = "Enable Analytics")]
    public bool EnableAnalytics { get; set; }

    // DateTime
    [Display(Name = "Launch Date")]
    public DateTime? LaunchDate { get; set; }

    // Guid
    [Display(Name = "Tracking ID")]
    public Guid TrackingId { get; set; } = Guid.NewGuid();

    // URL — using Uri type
    [Display(Name = "External API URL")]
    public Uri? ExternalApiUrl { get; set; }

    // URL — using string with [Url] attribute
    [Url]
    [Display(Name = "Website URL")]
    public string? WebsiteUrl { get; set; }

    // Multi-value string list
    [Display(Name = "Allowed Domains")]
    public IList<string> AllowedDomains { get; set; } = new List<string>();
}
```

### Notes

- All value types (`int`, `bool`, `DateTime`, `DateTimeOffset`, `Guid`) are automatically treated as **required** unless declared as nullable (`int?`, `DateTime?`, etc.)
- `IList<string>` and `List<string>` are interchangeable — both produce the same array UI
- `Uri` and `string + [Url]` both produce a URL input field with `https://` placeholder; use `Uri` when you want a typed property, `string` when you need plain string access
- `EPiServer.Url` opens the Optimizely URL picker (link to pages, media, or external URLs), while `ContentReference` opens the Optimizely content picker for selecting a content item directly
- Properties with unsupported types are **skipped with a warning** in the logs — they will not appear in the admin form



Create a class decorated with the `[SettingsGroup]` attribute:

```csharp
using Avantibit.Optimizely.CustomSettings.Attributes;
using System.ComponentModel.DataAnnotations;

namespace YourProject.Settings;

[SettingsGroup(
    Name = "Site Configuration",
    Description = "General site configuration settings",
    SortOrder = 100)]
public class SiteSettings
{
    [Display(Name = "Items Per Page")]
    [Range(1, 100)]
    public int ItemsPerPage { get; set; } = 10;

    [Display(Name = "Enable Analytics")]
    public bool EnableAnalytics { get; set; } = true;

    [Display(Name = "Default Language")]
    [Required]
    public string DefaultLanguage { get; set; } = "en";

    [Display(Name = "Contact Email")]
    [EmailAddress]
    public string ContactEmail { get; set; } = "contact@example.com";

    [Display(Name = "Max Upload Size (MB)")]
    [Range(1, 500)]
    public int MaxUploadSize { get; set; } = 50;

    [Display(Name = "Enable Maintenance Mode")]
    public bool EnableMaintenanceMode { get; set; } = false;
}
```

### 2. Inject and Use Settings

In your controllers, views, or services, inject `ICustomSettingsService<T>`:

```csharp
using Avantibit.Optimizely.CustomSettings.Configuration;

public class HomeController : Controller
{
    private readonly ICustomSettingsService<SiteSettings> _settingsService;

    public HomeController(ICustomSettingsService<SiteSettings> settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<IActionResult> Index()
    {
        // Get settings for current site and language
        var settings = await _settingsService.GetAsync();
        
        ViewBag.SiteName = settings.SiteName;
        ViewBag.ItemsPerPage = settings.ItemsPerPage;
        
        return View();
    }
}
```

### 3. Save Settings Programmatically

```csharp
public async Task UpdateSettings()
{
    var settings = await _settingsService.GetAsync();
    settings.SiteName = "Updated Site Name";
    settings.ItemsPerPage = 20;
    
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

```csharp
[SettingsGroup(
    Name = "Site Configuration",
    Description = "General site configuration settings",
    SortOrder = 100,
    AuthorizationPolicy = "AdminsOnly")]  // must match a registered policy name
public class SiteSettings
{
    [Display(Name = "Site Name")]
    public string SiteName { get; set; } = "My Site";
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
Marks a property to fallback to the master/default language value when the current language value is null.

**Usage:**
```csharp
[FallbackToMasterLanguage]
public string SiteName { get; set; }
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
Service for managing settings cache.

```csharp
T? Get<T>(string groupName, Guid? siteId, string? languageCode) where T : class;
void Set<T>(string groupName, T value, Guid? siteId, string? languageCode) where T : class;
void Remove(string groupName, Guid? siteId, string? languageCode);
void Clear();
```

#### `ISettingsRepository`
Repository interface for data access.

```csharp
Task<string?> GetSettingsAsync(string groupName, Guid? siteId, string? languageCode, CancellationToken cancellationToken = default);
Task SaveSettingsAsync(string groupName, string jsonData, Guid? siteId, string? languageCode, CancellationToken cancellationToken = default);
Task<bool> DeleteSettingsAsync(string groupName, Guid? siteId, string? languageCode, CancellationToken cancellationToken = default);
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
- `SettingsDiscoveryHostedService` - Startup discovery (hosted service)

## Admin Interface

Once configured, the custom settings management interface is available in the Optimizely CMS admin menu:

1. Log in to your Optimizely CMS admin interface
2. Navigate to **Add-ons** > **Custom Settings** (or your configured menu location)
3. Select the settings group you want to manage
4. Choose the site and language
5. Edit the settings using the generated form
6. Click **Save** to persist changes

The admin interface provides:
- Dropdown selection for site and language
- Auto-generated forms based on your settings class
- Real-time validation based on data annotations
- JSON editor for advanced scenarios

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
1. The cache automatically invalidates on save
2. If issues persist, inject `ISettingsCacheService` and call `Clear()`
3. Check for multiple application instances without distributed cache

---

#### Issue: Language fallback not working

**Problem:** Properties with `[FallbackToMasterLanguage]` don't fallback.

**Solution:**
1. Ensure the master/default language is properly configured in Optimizely
2. Verify that the fallback attribute is applied to the property
3. The property must be null or empty for fallback to occur

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

1. **Caching:** Settings are cached by default. Cache keys include site, language, and group name.
2. **Discovery:** Settings discovery runs once at startup. Performance impact is minimal.
3. **Database queries:** Indexed by GroupName, SiteId, and LanguageCode for optimal performance.