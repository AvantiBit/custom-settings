using Avantibit.Optimizely.CustomSettings.Caching;
using Avantibit.Optimizely.CustomSettings.Configuration;
using Avantibit.Optimizely.CustomSettings.Discovery;
using Avantibit.Optimizely.CustomSettings.Infrastructure;
using Avantibit.Optimizely.CustomSettings.Optimizely.Controllers;
using Avantibit.Optimizely.CustomSettings.Persistence.Abstractions;
using Avantibit.Optimizely.CustomSettings.Persistence.EfCore.SqlServer;
using Avantibit.Optimizely.CustomSettings.Schema;
using Avantibit.Optimizely.CustomSettings.Synchronization;
using Avantibit.Optimizely.CustomSettings.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Avantibit.Optimizely.CustomSettings.Extensions;

/// <summary>
/// Extension methods for configuring custom settings services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds custom settings management services to the service collection.
    /// This will automatically discover all settings groups decorated with [SettingsGroup] at startup.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration containing connection string and other settings.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the EPiServerDB connection string is not found or database configuration fails.</exception>
    public static IServiceCollection AddCustomSettings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("EPiServerDB");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'EPiServerDB' not found in configuration. " +
                "Ensure appsettings.json contains a valid 'EPiServerDB' connection string.");
        }

        try
        {
            //Registers DbContext with connection string
            services.AddDbContext<CustomSettingsDbContext>(options =>
            {
                options.UseSqlServer(
                    connectionString,
                    sqlOptions => sqlOptions.MigrationsHistoryTable(
                        "CustomSettingsMigrations",
                        "dbo"));
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to configure database context for Custom Settings. " +
                "Check that the connection string is valid and SQL Server is accessible.",
                ex);
        }
        // Register the default authorization policy (fail-closed: editor or admin required)
        services.AddAuthorization(options =>
        {
            options.AddPolicy(CustomSettingsConstants.DefaultPolicyName, policy =>
                policy.RequireRole(
                    CustomSettingsConstants.RoleWebAdmins,
                    CustomSettingsConstants.RoleWebEditors,
                    CustomSettingsConstants.RoleCmsAdmins,
                    CustomSettingsConstants.RoleCmsEditors));
        });

        // Ensure plugin controllers are discoverable
        services.AddControllersWithViews()
            .AddApplicationPart(typeof(CustomSettingsController).Assembly);

        // Ensure views can be discovered from /Optimizely/Views/...
        services.Configure<RazorViewEngineOptions>(options =>
            options.ViewLocationExpanders.Add(new CustomSettingsViewLocationExpander()));

        //Registers settings repository for data access
        services.AddScoped<ISettingsRepository, SettingsRepository>();

        //Registers settings cache as singleton for optimal read performance
        services.AddSingleton<ISettingsCacheService, SettingsCacheService>();

        //Registers generic settings service for typed access
        services.AddScoped(typeof(ICustomSettingsService<>), typeof(CustomSettingsService<>));

        //Registers settings discovery service as singleton so discovery runs once at startup
        services.AddSingleton<ISettingsDiscoveryService, SettingsDiscoveryService>();

        //Registers schema builder for generating JSON schemas from settings types
        services.AddSingleton<ISettingsSchemaBuilder, SettingsSchemaBuilder>();

        //Registers a factory for creating settings view models, used by controllers and views
        services.AddSingleton<ISettingsViewModelFactory, SettingsViewModelFactory>();

        //Registers a hosted service to perform settings discovery and migration at application startup
        services.AddHostedService<CustomSettingsMigrationHostedService>();

        //Registers a validator for settings groups, ensuring data integrity before saving
        services.AddHostedService<SettingsDiscoveryHostedService>();

        //Registers cache polling service for cross-server synchronization
        services.Configure<SettingsCacheOptions>(_ => { });
        services.AddHostedService<SettingsCachePollingService>();

        return services;
    }

    /// <summary>
    /// Adds custom settings management services with custom cache options.
    /// </summary>
    public static IServiceCollection AddCustomSettings(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<SettingsCacheOptions> configureCacheOptions)
    {
        services.AddCustomSettings(configuration);
        services.Configure(configureCacheOptions);
        return services;
    }
}