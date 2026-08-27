using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchSelectorTests;

public class BaseCatchSelectorTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid PikeCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid TroutCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    protected static readonly DateTimeOffset CaughtOn = DateTimeOffset.Parse("2026-08-27T07:30:00Z");

    protected static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static CatchModel Catch(Guid catchId, string? speciesName, DateTimeOffset? caughtOn = null)
    {
        return new CatchModel(
            catchId,
            caughtOn ?? CaughtOn,
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)],
            speciesName,
            UserId: OwnerUserId);
    }
}
