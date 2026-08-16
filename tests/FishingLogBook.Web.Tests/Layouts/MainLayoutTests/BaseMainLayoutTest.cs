using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Features.Authentication.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.MainLayoutTests;

public class BaseMainLayoutTest
{
    protected static BunitContext CreateContext(bool isAuthenticated = false)
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
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }
}
