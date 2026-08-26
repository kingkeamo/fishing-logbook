using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Authentication.Services;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Synchronisers;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.MainLayoutTests;

public class BaseMainLayoutTest
{
    protected const string SignedInEmail = "tester@example.test";

    protected static BunitContext CreateContext(
        bool isAuthenticated = false,
        ICatchSynchroniser? catchSynchroniser = null,
        IDiagnosticSynchroniser? diagnosticSynchroniser = null,
        IProfileSummaryProvider? profileSummary = null,
        IAppUpdateService? appUpdate = null,
        INetworkService? network = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton<ISignedInUserDisplayService, SignedInUserDisplayService>();
        context.Services.AddSingleton(new AuthConfig
        {
            Authority = "https://cognito-idp.eu-west-2.amazonaws.com/eu-west-2_test",
            ClientId = "test-client",
            HostedUiDomain = "https://test.auth.eu-west-2.amazoncognito.com",
            ApiResource = "https://api.test",
            ApiScope = "https://api.test/access"
        });
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

        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddSingleton(
            catchSynchroniser ?? Substitute.For<ICatchSynchroniser>());
        context.Services.AddSingleton(
            diagnosticSynchroniser ?? Substitute.For<IDiagnosticSynchroniser>());
        context.Services.AddSingleton(Substitute.For<ILoggingService>());
        context.Services.AddSingleton(appUpdate ?? Substitute.For<IAppUpdateService>());
        context.Services.AddSingleton(profileSummary ?? QuietProfileSummary());
        context.Services.AddSingleton(network ?? Network(isOnline: true));
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static IProfileSummaryProvider QuietProfileSummary(ProfileSummaryModel? summary = null)
    {
        var provider = Substitute.For<IProfileSummaryProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(summary ?? ProfileSummaryModel.Empty);
        return provider;
    }

    protected static INetworkService Network(bool isOnline)
    {
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(isOnline);
        return network;
    }
}
