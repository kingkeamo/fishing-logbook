using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Pages.OfflineCatchList;
using FishingLogBook.Web.Features.OfflineAccess.Components.OfflineRouteView;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Components.OfflineRouteViewTests;

public class WhenTestingRender : BaseOfflineRouteViewTest
{
    [Fact]
    public async Task ItShouldReturnToLandingWithoutInstantiatingThePageWhenLocked()
    {
        // Arrange
        await using var context = CreateContext();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/offline/catches");
        var routeData = new RouteData(typeof(OfflineCatchList), new Dictionary<string, object?>());

        // Act
        var cut = context.Render<OfflineRouteView>(parameters => parameters.Add(view => view.RouteData, routeData));

        // Assert
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().Be("http://localhost/");
        cut.FindAll("#offline-catch-list-title").Should().BeEmpty();
    }
}
