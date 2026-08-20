using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Features.Authentication.Components.UserMenu;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Authentication.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Authentication.Components.UserMenuTests;

public class BaseUserMenuTest
{
    protected const string SignedInEmail = "angler@example.test";

    protected static BunitContext CreateContext(
        IProfileSummaryProvider profileSummary,
        bool isAuthenticated = true)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton<ISignedInUserDisplayService, SignedInUserDisplayService>();
        context.Services.AddSingleton(profileSummary);
        context.Services.AddSingleton(new AuthConfig
        {
            Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_test",
            ClientId = "test-client",
            HostedUiDomain = "https://test.auth.eu-west-2.amazoncognito.com",
            ApiResource = "https://api.test",
            ApiScope = "https://api.test/access"
        });
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        var authorization = context.AddAuthorization();
        if (isAuthenticated)
        {
            authorization.SetAuthorized(SignedInEmail);
            authorization.SetClaims(new System.Security.Claims.Claim("email", SignedInEmail));
        }
        else
        {
            authorization.SetNotAuthorized();
        }

        return context;
    }

    protected static IProfileSummaryProvider ProfileSummary(ProfileSummaryModel? summary = null)
    {
        var provider = Substitute.For<IProfileSummaryProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>()).Returns(summary ?? ProfileSummaryModel.Empty);
        return provider;
    }

    protected static ProfileSummaryModel WithPhotograph(string url = "https://cdn.test/photo.jpg")
    {
        return new ProfileSummaryModel(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Eamonn",
            url);
    }

    protected static (IRenderedComponent<UserMenu> Menu, IRenderedComponent<MudPopoverProvider> Popover)
        RenderMenu(BunitContext context)
    {
        var popover = context.Render<MudPopoverProvider>();
        var menu = context.Render<UserMenu>();
        return (menu, popover);
    }
}
