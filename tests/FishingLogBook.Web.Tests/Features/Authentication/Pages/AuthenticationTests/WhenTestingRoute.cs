using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using AuthenticationPage = FishingLogBook.Web.Features.Authentication.Pages.Authentication.Authentication;

namespace FishingLogBook.Web.Tests.Features.Authentication.Pages.AuthenticationTests;

public class WhenTestingRoute
{
    [Fact]
    public void ItShouldMapTheOidcCallbackPath()
    {
        // Arrange
        // Act
        var route = typeof(AuthenticationPage)
            .GetCustomAttributes(inherit: false)
            .OfType<RouteAttribute>()
            .Single();

        // Assert
        route.Template.Should().Be("/authentication/{action}");
    }
}
