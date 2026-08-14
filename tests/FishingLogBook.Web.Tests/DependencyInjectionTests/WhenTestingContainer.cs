using AwesomeAssertions;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Offline;
using FishingLogBook.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Tests.DependencyInjectionTests;

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
        injectedTypes.Should().Contain(typeof(ICultureService));
        injectedTypes.Should().Contain(typeof(IStringLocalizer<UiStrings>));
        resolve.Should().NotThrow();
    }
}
