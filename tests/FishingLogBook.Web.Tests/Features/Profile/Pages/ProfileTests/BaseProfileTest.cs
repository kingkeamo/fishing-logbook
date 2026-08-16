using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class BaseProfileTest
{
    protected static BunitContext CreateContext(
        IProfileClient profileClient,
        ILocationService? locationService = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(profileClient);
        context.Services.AddSingleton(locationService ?? Substitute.For<ILocationService>());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        var authorization = context.AddAuthorization();
        authorization.SetAuthorized("tester@example.test");
        return context;
    }

    protected static ProfileDto EmptyProfile(Guid? userId = null)
    {
        return new ProfileDto(
            userId ?? Guid.NewGuid(),
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            true,
            false,
            false,
            false,
            false);
    }
}
