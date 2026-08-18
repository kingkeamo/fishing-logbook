using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchLocationPrivacyTests;

public class BaseCatchLocationPrivacyTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected static BunitContext CreateContext(ICatchStore store, ICatchClient? client = null, ILocalCatchOwnerService? owner = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(client ?? Substitute.For<ICatchClient>());
        context.Services.AddSingleton(owner ?? SignedInOwner());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static ILocalCatchOwnerService SignedInOwner()
    {
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerUserId);
        return owner;
    }

    protected static CatchModel LocatedCatch(Guid catchId, CatchLocationModel? location)
    {
        return new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg, [1])],
            Location: location,
            UserId: OwnerUserId,
            AnglerUserId: OwnerUserId,
            RecordedByUserId: OwnerUserId);
    }

    protected static CatchModel LocatedCatch(Guid catchId, string visibility = LocationDefaults.Private)
    {
        return LocatedCatch(
            catchId,
            new CatchLocationModel(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                LocationDefaults.DeviceGps,
                visibility,
                LocationDefaults.ConsentVersion));
    }
}
