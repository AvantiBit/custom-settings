# Avantibit.Optimizely.CustomSettings

Type-safe custom settings management for Optimizely CMS. Define strongly-typed settings classes, manage their values per site and language from a built-in admin UI, and read them through a cached, injectable service. Settings are persisted with Entity Framework Core (SQL Server) and discovered automatically at startup.

## Version compatibility

**This package (2.x) targets Optimizely CMS 13 on .NET 10.** For Optimizely CMS 12, use version 1.x.

| Package version | Optimizely CMS | .NET |
|---|---|---|
| 2.x | CMS 13 | .NET 10 |
| 1.x | CMS 12 | .NET 10 |

## Installation

```bash
dotnet add package Avantibit.Optimizely.CustomSettings --version 2.*
```

## Quick start

Register the services in `Startup.cs` (requires an `EPiServerDB` connection string):

```csharp
using Avantibit.Optimizely.CustomSettings.Extensions;

public void ConfigureServices(IServiceCollection services)
{
    services
        .AddCms()
        .AddCustomSettings(_configuration);
}
```

Define a settings class:

```csharp
[SettingsGroup(Name = "Site Settings")]
public class SiteSettings
{
    [Display(Name = "Site Name")]
    public string SiteName { get; set; } = "My Site";
}
```

Consume it anywhere via dependency injection:

```csharp
public class HomeController(ICustomSettingsService<SiteSettings> settingsService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var settings = await settingsService.GetAsync();
        return View(settings);
    }
}
```

Editors manage the values from the **Custom Settings** menu in the Optimizely CMS admin interface.

## Features

- Strongly-typed settings classes with data-annotation validation
- Multi-site and multi-language values with master-language fallback
- Built-in caching with cross-server cache synchronization
- Admin UI integrated into the Optimizely CMS shell
- Automatic discovery of settings groups at startup
- Role-based access control per settings group

## Documentation

Full documentation, supported property types, and a sample site:
https://github.com/AvantiBit/custom-settings

## License

Apache-2.0 — Copyright © AvantiBit AB
