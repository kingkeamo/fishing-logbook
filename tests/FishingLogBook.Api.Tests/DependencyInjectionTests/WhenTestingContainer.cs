using AwesomeAssertions;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.SystemStatus;
using FishingLogBook.Application.TestCatches;
using MediatR;
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
        injectedTypes.Should().Contain(typeof(TestCatchService));
        injectedTypes.Should().Contain(typeof(ICurrentUser));
        resolve.Should().NotThrow();
    }

    [Fact]
    public void ItShouldResolveTheCqrsPipeline()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();

        // Act
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var userIdentityService = scope.ServiceProvider.GetRequiredService<IUserIdentityService>();

        // Assert
        mediator.Should().NotBeNull();
        userIdentityService.Should().NotBeNull();
    }

    [Fact]
    public void ItShouldResolveDbConnectionFactory_WhenTestHostProvidesConnectionString()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();

        // Act
        Action resolve = () => _ = scope.ServiceProvider.GetRequiredService<ISystemRepository>();

        // Assert
        resolve.Should().NotThrow();
    }
}
