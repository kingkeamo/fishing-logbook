using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Modals.LocationPrivacy;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Modals.LocationPrivacyModalTests;

public class BaseLocationPrivacyModalTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected static BunitContext CreateContext(
        ICatchStore store,
        ICatchClient? client = null,
        ILocalCatchOwnerService? owner = null,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(client ?? Substitute.For<ICatchClient>());
        context.Services.AddSingleton(owner ?? SignedInOwner());
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> ShowModalAsync(
        BunitContext context,
        Guid catchId)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<LocationPrivacyModal>
        {
            { modal => modal.Model, new LocationPrivacyModalModel(catchId) }
        };
        var dialog = await dialogs.ShowAsync<LocationPrivacyModal>(
            parameters,
            new DialogOptions
            {
                CloseButton = true,
                CloseOnEscapeKey = true,
                FullWidth = true,
                MaxWidth = MaxWidth.Small
            });
        return (cut, dialog);
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

    protected static async Task ShouldHaveClosedAsSaved(IDialogReference dialog)
    {
        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeFalse();
        result.Data.Should().BeOfType<LocationPrivacyModalResult>().Which.Saved.Should().BeTrue();
    }
}
