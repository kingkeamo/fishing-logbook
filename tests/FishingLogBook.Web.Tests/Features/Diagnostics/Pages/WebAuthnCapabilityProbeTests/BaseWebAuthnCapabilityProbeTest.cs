using Bunit;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Pages.WebAuthnCapabilityProbeTests;

public class BaseWebAuthnCapabilityProbeTest
{
    protected static BunitContext CreateContext(
        IWebAuthnCapabilityProbeService probe,
        WebAuthnCapabilityProbeResultModel? status = null)
    {
        probe.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(status ?? new WebAuthnCapabilityProbeResultModel
        {
            WebAuthnAvailable = true,
            PlatformAuthenticatorAvailable = true,
            HasProbeMetadata = true,
            Outcome = "ready"
        });

        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(probe);
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }
}
