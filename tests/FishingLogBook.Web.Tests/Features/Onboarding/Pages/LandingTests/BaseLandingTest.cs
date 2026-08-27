using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Authentication.Services;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Synchronisers;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Onboarding.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.LandingTests;

public class BaseLandingTest
{
    protected static BunitContext CreateContext(
        IOnboardingService onboarding,
        bool isAuthenticated = true,
        AuthenticationStateProvider? authenticationStateProvider = null,
        ILoggingService? logging = null,
        IOfflineAccessDeviceService? offlineAccessDevice = null,
        INetworkService? network = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(onboarding);
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        context.Services.AddSingleton(offlineAccessDevice ?? OfflineAccessDevice(hasReadyEntitlement: false));
        context.Services.AddSingleton(network ?? Network(isOnline: true));
        context.Services.AddScoped<IOfflineOwnerContextService, OfflineOwnerContextService>();
        var authorization = context.AddAuthorization();
        if (isAuthenticated)
        {
            authorization.SetAuthorized("angler@example.test");
        }
        else
        {
            authorization.SetNotAuthorized();
        }

        if (authenticationStateProvider is not null)
        {
            context.Services.AddSingleton(authenticationStateProvider);
        }

        return context;
    }

    protected static AuthenticationStateProvider Authentication(Task<AuthenticationState> authentication)
    {
        var provider = Substitute.For<AuthenticationStateProvider>();
        provider.GetAuthenticationStateAsync().Returns(authentication);
        return provider;
    }

    protected static void AddApplicationShell(BunitContext context)
    {
        context.Services.AddSingleton<ISignedInUserDisplayService, SignedInUserDisplayService>();
        context.Services.AddSingleton(new AuthConfig
        {
            Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_test",
            ClientId = "test-client",
            HostedUiDomain = "https://test.auth.eu-west-2.amazoncognito.com",
            ApiResource = "https://api.test",
            ApiScope = "https://api.test/access"
        });
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddSingleton(Substitute.For<ICatchSynchroniser>());
        context.Services.AddSingleton(Substitute.For<ILogbookSynchroniser>());
        context.Services.AddSingleton(Substitute.For<IDiagnosticSynchroniser>());
        context.Services.AddSingleton(Substitute.For<IAppUpdateService>());
        var profile = Substitute.For<IProfileSummaryProvider>();
        profile.GetAsync(Arg.Any<CancellationToken>()).Returns(ProfileSummaryModel.Empty);
        context.Services.AddSingleton(profile);
        context.Services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();
    }

    protected static IOfflineAccessDeviceService OfflineAccessDevice(bool hasReadyEntitlement)
    {
        var device = Substitute.For<IOfflineAccessDeviceService>();
        device.HasReadyEntitlementAsync(Arg.Any<CancellationToken>()).Returns(
            new OfflineAccessAvailabilityModel(
                hasReadyEntitlement ? "ready" : "not-configured",
                hasReadyEntitlement ? "ready-record-found" : "no-records"));
        return device;
    }

    protected static INetworkService Network(bool isOnline)
    {
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(isOnline);
        return network;
    }

    protected static IOnboardingService Onboarding(bool completed)
    {
        var onboarding = Substitute.For<IOnboardingService>();
        onboarding.IsCompletedAsync(Arg.Any<CancellationToken>()).Returns(completed);
        return onboarding;
    }
}
