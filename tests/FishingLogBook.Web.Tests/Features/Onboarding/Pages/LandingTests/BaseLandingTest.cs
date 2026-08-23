using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Onboarding.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.LandingTests;

public class BaseLandingTest
{
    protected static BunitContext CreateContext(
        IOnboardingService onboarding,
        bool isAuthenticated = true,
        IWebAuthnCapabilityProbeService? probe = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(onboarding);
        context.Services.AddSingleton(probe ?? Probe(hasMetadata: false));
        var authorization = context.AddAuthorization();
        if (isAuthenticated)
        {
            authorization.SetAuthorized("angler@example.test");
        }
        else
        {
            authorization.SetNotAuthorized();
        }

        return context;
    }

    protected static IWebAuthnCapabilityProbeService Probe(bool hasMetadata)
    {
        var probe = Substitute.For<IWebAuthnCapabilityProbeService>();
        probe.HasMetadataAsync(Arg.Any<CancellationToken>()).Returns(hasMetadata);
        return probe;
    }

    protected static IOnboardingService Onboarding(bool completed)
    {
        var onboarding = Substitute.For<IOnboardingService>();
        onboarding.IsCompletedAsync(Arg.Any<CancellationToken>()).Returns(completed);
        return onboarding;
    }
}
