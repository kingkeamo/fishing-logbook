using System.Reflection;
using AwesomeAssertions;
using FishingLogBook.Web.Features.OfflineAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.OfflineArchitectureTests;

public class WhenTestingDependencies : BaseOfflineArchitectureTest
{
    private static readonly string[] ForbiddenDependencyNames =
    [
        "AuthenticationStateProvider",
        "IAuthorizationService",
        "ICatchClient",
        "ILocalCatchOwnerService",
        "ICatchSynchroniser",
        "IAnglerPreferencesProvider",
        "INetworkService",
        "IOfflineAccessPreferenceClient"
    ];

    [Fact]
    public void ItShouldKeepTheOfflineSurfaceFreeOfOnlineAuthenticationApiAndSyncDependencies()
    {
        // Arrange
        var injectedTypes = OfflineSurfaceTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null)
            .Select(property => property.PropertyType.Name)
            .ToArray();

        // Act
        var forbidden = injectedTypes.Intersect(ForbiddenDependencyNames).ToArray();

        // Assert
        forbidden.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldMarkEveryOfflinePageForTheDedicatedGuardWithoutOnlineAuthorization()
    {
        // Arrange
        var pages = OfflineSurfaceTypes.Where(type => type != typeof(FishingLogBook.Web.Layouts.OfflineLayout.OfflineLayout)).ToArray();

        // Act
        var offlineMarkers = pages.Select(type => type.GetCustomAttribute<OfflineRouteAttribute>()).ToArray();
        var onlineAuthorization = pages.Select(type => type.GetCustomAttribute<AuthorizeAttribute>()).ToArray();

        // Assert
        offlineMarkers.Should().NotContainNulls();
        onlineAuthorization.Should().OnlyContain(attribute => attribute == null);
    }
}
