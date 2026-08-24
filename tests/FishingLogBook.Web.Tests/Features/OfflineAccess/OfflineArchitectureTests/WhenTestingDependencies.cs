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
            .Select(property => property.PropertyType)
            .ToArray();

        // Act
        var forbidden = injectedTypes
            .Where(type => ForbiddenDependencyNames.Contains(type.Name)
                || IsOnlineDependencyCategory(type))
            .Select(type => type.FullName)
            .ToArray();

        // Assert
        forbidden.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldMarkEveryOfflinePageForTheDedicatedGuardWithoutOnlineAuthorization()
    {
        // Arrange
        var pages = OfflinePageTypes;

        // Act
        var offlineMarkers = pages.Select(type => type.GetCustomAttribute<OfflineRouteAttribute>()).ToArray();
        var onlineAuthorization = pages.Select(type => type.GetCustomAttribute<AuthorizeAttribute>()).ToArray();

        // Assert
        offlineMarkers.Should().NotContainNulls();
        onlineAuthorization.Should().OnlyContain(attribute => attribute == null);
    }

    private static bool IsOnlineDependencyCategory(Type type)
    {
        var namespaceName = type.Namespace ?? string.Empty;
        return type.Name.EndsWith("Client", StringComparison.Ordinal)
            || type.Name.EndsWith("Synchroniser", StringComparison.Ordinal)
            || namespaceName.Contains(".Clients", StringComparison.Ordinal)
            || namespaceName.Contains(".Synchronisers", StringComparison.Ordinal)
            || namespaceName.Contains(".Authentication", StringComparison.Ordinal)
            || namespaceName.Contains(".Authorization", StringComparison.Ordinal)
            || namespaceName.Contains(".Browser.Network", StringComparison.Ordinal)
            || namespaceName.Contains(".Providers", StringComparison.Ordinal);
    }
}
