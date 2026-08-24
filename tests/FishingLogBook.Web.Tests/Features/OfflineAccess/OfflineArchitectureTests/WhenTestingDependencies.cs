using System.Reflection;
using AwesomeAssertions;
using FishingLogBook.Web.Common.Routing;
using FishingLogBook.Web.Features.OfflineAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using OfflineDiagnosticsPage = FishingLogBook.Web.Features.Diagnostics.Pages.OfflineDiagnostics.OfflineDiagnostics;
using OfflineLayoutPage = FishingLogBook.Web.Layouts.OfflineLayout.OfflineLayout;
using PublicDiagnosticsLayout = FishingLogBook.Web.Layouts.PublicLayout.PublicLayout;

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
    public void ItShouldKeepOfflinePagesAndEditorsFreeOfOnlineAuthenticationApiAndSyncDependencies()
    {
        // Arrange
        var injectedTypes = OfflineSurfaceTypes
            .Where(type => type != typeof(OfflineLayoutPage))
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
    public void ItShouldLimitTheOfflineLayoutToTheReconnectGateway()
    {
        // Arrange
        var injectedTypes = typeof(OfflineLayoutPage)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null)
            .Select(property => property.PropertyType)
            .ToArray();

        // Act
        var directOnlineDependencies = injectedTypes.Where(type =>
            IsOnlineDependencyCategory(type)
            && type.Name != "IOfflineReconnectService");

        // Assert
        directOnlineDependencies.Should().BeEmpty();
        injectedTypes.Should().Contain(type => type.Name == "IOfflineReconnectService");
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

    [Fact]
    public void ItShouldKeepOfflineDiagnosticsPublicLocalAndIndependentOfOfflineUnlock()
    {
        // Arrange
        Type[] surfaceTypes = [typeof(OfflineDiagnosticsPage), typeof(PublicDiagnosticsLayout)];
        var pageType = surfaceTypes[0];
        var injectedTypes = surfaceTypes
            .SelectMany(type => type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null))
            .Select(property => property.PropertyType)
            .ToArray();

        // Act
        var isPublic = pageType.GetCustomAttribute<PublicRouteAttribute>() is not null;
        var isOfflineOwnerRoute = pageType.GetCustomAttribute<OfflineRouteAttribute>() is not null;
        var requiresAuthorization = pageType.GetCustomAttribute<AuthorizeAttribute>() is not null;
        var forbidden = injectedTypes.Where(type =>
            type.Name == "IOfflineOwnerContextService"
            || ForbiddenDependencyNames.Contains(type.Name)
            || IsOnlineDependencyCategory(type));

        // Assert
        isPublic.Should().BeTrue();
        isOfflineOwnerRoute.Should().BeFalse();
        requiresAuthorization.Should().BeFalse();
        forbidden.Should().BeEmpty();
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
