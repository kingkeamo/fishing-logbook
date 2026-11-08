using Bunit;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Authentication.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Authentication.Components.RedirectToLoginTests;

public abstract class BaseRedirectToLoginTest
{
    protected static BunitContext CreateContext(string[] socialProviders)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton<ISignInNavigationService, SignInNavigationService>();
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(false));
        context.Services.AddCascadingAuthenticationState();
        context.Services.AddSingleton(new AuthConfig { SocialProviders = socialProviders });
        return context;
    }
}
