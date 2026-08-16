using AwesomeAssertions;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using FishingLogBook.Web.Features.SystemStatus.Services;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Tests.DependencyInjection;

public class WhenTestingContainer : BaseDependencyInjectionTest
{
    [Fact]
    public void ItShouldBuildAndValidateTheServiceProvider()
    {
        // Arrange
        Action build = () =>
        {
            using var provider = CreateProvider();
        };

        // Act
        // Assert
        build.Should().NotThrow();
    }

    [Fact]
    public void ItShouldResolveServicesInjectedByComponents()
    {
        // Arrange
        var injectedTypes = GetComponentInjectedServiceTypes();

        // Act
        Action resolve = () =>
        {
            using var provider = CreateProvider();
            using var scope = provider.CreateScope();
            foreach (var type in injectedTypes)
            {
                scope.ServiceProvider.GetRequiredService(type);
            }
        };

        // Assert
        injectedTypes.Should().Contain(typeof(ISystemStatusClient));
        injectedTypes.Should().Contain(typeof(ITestCatchStore));
        injectedTypes.Should().Contain(typeof(ITestCatchPhotoStore));
        injectedTypes.Should().Contain(typeof(ITestCatchSynchroniser));
        injectedTypes.Should().Contain(typeof(ICultureService));
        injectedTypes.Should().Contain(typeof(ILoggingService));
        injectedTypes.Should().Contain(typeof(ILocationService));
        injectedTypes.Should().Contain(typeof(IStringLocalizer<UiStrings>));
        resolve.Should().NotThrow();
    }
}
