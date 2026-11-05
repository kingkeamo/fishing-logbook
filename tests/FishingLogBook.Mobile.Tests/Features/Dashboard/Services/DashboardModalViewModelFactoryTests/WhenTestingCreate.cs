using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Mobile.Features.Dashboard.Services;
using FishingLogBook.Mobile.Features.Dashboard.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.Services.DashboardModalViewModelFactoryTests;

public class WhenTestingCreate
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new UiTextService(CultureInfo.GetCultureInfo("en-GB")));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(DateTimeOffset.Parse("2026-11-05T08:00Z"), TimeZoneInfo.Utc));
        services.AddDashboardModalViewModels();
        services.Single(service => service.ServiceType == typeof(DashboardFilterViewModel)).Lifetime.Should().Be(ServiceLifetime.Transient);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void ItShouldCreateAFreshFilterViewModelForEveryModalOpening()
    {
        // Arrange
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<IDashboardModalViewModelFactory>();

        // Act
        var first = factory.CreateFilters();
        var second = factory.CreateFilters();

        // Assert
        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void ItShouldNeverLetAnotherOpeningChangeAnExistingDraft()
    {
        // Arrange
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<IDashboardModalViewModelFactory>();
        var first = factory.CreateFilters();
        first.Initialize(new DashboardFilterSelectionModel { Preset = DashboardDatePresetEnum.Custom, Species = "Pike", From = new DateTime(2026, 3, 1), To = new DateTime(2026, 3, 2) }, new DashboardFilterOptionsModel(["Pike"], [], []));

        // Act
        var second = factory.CreateFilters();
        second.Initialize(new DashboardFilterSelectionModel(), new DashboardFilterOptionsModel(["Perch"], [], []));
        second.SelectedSpecies = second.SpeciesOptions.Single(choice => choice.Value == "Perch");

        // Assert
        first.IsCustom.Should().BeTrue();
        first.SelectedSpecies!.Value.Should().Be("Pike");
        first.FromDate.Should().Be(new DateTime(2026, 3, 1));
        first.SpeciesOptions.Should().NotContain(choice => choice.Value == "Perch");
        second.IsCustom.Should().BeFalse();
        second.SelectedSpecies!.Value.Should().Be("Perch");
    }
}
