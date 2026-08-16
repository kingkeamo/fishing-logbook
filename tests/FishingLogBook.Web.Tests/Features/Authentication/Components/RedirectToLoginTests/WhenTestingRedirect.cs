using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Authentication.Components.RedirectToLogin;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Authentication.Components.RedirectToLoginTests;

public class WhenTestingRedirect
{
    [Fact]
    public async Task ItShouldNavigateToLogin()
    {
        // Arrange
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(false));
        context.Services.AddCascadingAuthenticationState();

        // Act
        context.Render<RedirectToLogin>();
        var uri = context.Services.GetRequiredService<NavigationManager>().Uri;

        // Assert
        uri.Should().Contain("authentication/login");
    }
}
