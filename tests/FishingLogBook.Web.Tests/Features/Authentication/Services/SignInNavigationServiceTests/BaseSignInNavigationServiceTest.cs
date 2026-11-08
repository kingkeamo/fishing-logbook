using Bunit;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Authentication.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Features.Authentication.Services.SignInNavigationServiceTests;

public abstract class BaseSignInNavigationServiceTest
{
    protected static SignInNavigationService CreateSut(BunitContext context, params string[] socialProviders)
    {
        return new SignInNavigationService(
            context.Services.GetRequiredService<NavigationManager>(),
            new AuthConfig { SocialProviders = socialProviders });
    }
}
