using AwesomeAssertions;
using FishingLogBook.Application.SystemStatus;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Api.Tests.DependencyInjectionTests;

public class WhenTestingContainer : BaseDependencyInjectionTest
{
    public WhenTestingContainer(DependencyInjectionApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public void ItShouldBuildAndValidateTheServiceProvider()
    {
        // Arrange
        Action resolveProvider = () => _ = Factory.Services;

        // Act
        // Assert
        resolveProvider.Should().NotThrow();
    }

    [Fact]
    public void ItShouldResolveServicesRequiredByEndpointHandlers()
    {
        // Arrange
        var injectedTypes = GetEndpointInjectedServiceTypes();

        // Act
        Action resolve = () =>
        {
            using var scope = Factory.Services.CreateScope();
            foreach (var type in injectedTypes)
            {
                scope.ServiceProvider.GetRequiredService(type);
            }
        };

        // Assert
        injectedTypes.Should().Contain(typeof(SystemStatusService));
        resolve.Should().NotThrow();
    }
}
