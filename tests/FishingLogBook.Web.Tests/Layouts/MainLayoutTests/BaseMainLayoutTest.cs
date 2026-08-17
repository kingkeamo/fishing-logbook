using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Features.Authentication.Services;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.MainLayoutTests;

public class BaseMainLayoutTest
{
    protected static BunitContext CreateContext(
        bool isAuthenticated = false,
        ICatchSynchroniser? catchSynchroniser = null,
        IDiagnosticSynchroniser? diagnosticSynchroniser = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton<ISignedInUserDisplayService, SignedInUserDisplayService>();
        var authorization = context.AddAuthorization();
        if (isAuthenticated)
        {
            authorization.SetAuthorized("tester@example.test");
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
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }
}
