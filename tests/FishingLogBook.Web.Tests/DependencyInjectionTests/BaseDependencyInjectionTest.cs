using System.Reflection;
using FishingLogBook.Web.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.DependencyInjectionTests;

public class BaseDependencyInjectionTest
{
    protected static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IJSRuntime>());
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddFishingLogBookWeb(
            new ApiConfig { BaseUrl = "https://example.test/" },
            new DiagnosticsClientConfig(),
            new Uri("https://example.test/"));

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    protected static IReadOnlyCollection<Type> GetComponentInjectedServiceTypes()
    {
        return typeof(App).Assembly
            .GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null)
            .Select(property => property.PropertyType)
            .Distinct()
            .ToArray();
    }
}
