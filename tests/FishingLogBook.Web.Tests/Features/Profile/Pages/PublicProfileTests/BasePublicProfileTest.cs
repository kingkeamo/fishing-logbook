using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.PublicProfileTests;

public class BasePublicProfileTest
{
    protected static BunitContext CreateContext(IProfileClient profileClient)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(profileClient);
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        var authorization = context.AddAuthorization();
        authorization.SetAuthorized("tester@example.test");
        return context;
    }
}
