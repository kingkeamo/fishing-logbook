using System.Globalization;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class BaseCatchListTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected static BunitContext CreateContext(
        ICatchStore store,
        ILocalCatchOwnerService? owner = null,
        ICatchSynchroniser? synchroniser = null,
        IModalService? modalService = null,
        ITimeService? time = null,
        IAnglerPreferencesProvider? anglerPreferences = null,
        ILoggingService? logging = null,
        ICatchClient? catchClient = null,
        INetworkService? network = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(owner ?? SignedInOwner());
        context.Services.AddSingleton(synchroniser ?? Substitute.For<ICatchSynchroniser>());
        context.Services.AddSingleton(modalService ?? Substitute.For<IModalService>());
        context.Services.AddSingleton(time ?? UtcTime());
        context.Services.AddSingleton(anglerPreferences ?? QuietAnglerPreferences());
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton(catchClient ?? EmptyCatchClient());
        context.Services.AddSingleton(network ?? OnlineNetwork());
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddSingleton<ICatchDateGroupingService, CatchDateGroupingService>();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static ICatchClient EmptyCatchClient()
    {
        var client = Substitute.For<ICatchClient>();
        client.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CatchViewDto>)[]);
        return client;
    }

    protected static INetworkService OnlineNetwork(bool isOnline = true)
    {
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(isOnline);
        return network;
    }

    protected static ILocalCatchOwnerService SignedInOwner(Guid? userId = null)
    {
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(userId ?? OwnerUserId);
        return owner;
    }

    protected static IAnglerPreferencesProvider QuietAnglerPreferences(
        WeightUnitEnum weightUnit = WeightUnitEnum.Kg,
        LengthUnitEnum lengthUnit = LengthUnitEnum.Cm)
    {
        var provider = Substitute.For<IAnglerPreferencesProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new AnglerPreferencesModel(
                new FishingCatalogueDto([], []),
                new FishingPreferencesDto([]),
                weightUnit,
                lengthUnit));
        return provider;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static ITimeService UtcTime()
    {
        return OffsetTime(TimeSpan.Zero);
    }

    protected static ITimeService OffsetTime(TimeSpan offset)
    {
        return TestTimeService.WithOffset(offset);
    }

    protected static ITimeService FixedTodayTime(string localToday)
    {
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var instant = call.Arg<DateTimeOffset>();
                return (DateTimeOffset.UtcNow - instant).Duration() < TimeSpan.FromMinutes(1)
                    ? $"{localToday}T12:00"
                    : instant.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
            });
        return time;
    }

    protected static CatchModel StoredCatch(
        Guid catchId,
        DateTimeOffset caughtOn,
        SyncStatus syncStatus = SyncStatus.Synchronised,
        string? speciesName = null,
        string? method = null,
        string? baitOrLure = null,
        decimal? weight = null,
        decimal? length = null,
        CatchLocationModel? location = null,
        Guid? anglerUserId = null,
        Guid? recordedByUserId = null,
        bool withPhotograph = true)
    {
        return new CatchModel(
            catchId,
            caughtOn,
            withPhotograph
                ? [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3])]
                : [],
            SpeciesName: speciesName,
            Location: location,
            UserId: OwnerUserId,
            SyncStatus: syncStatus,
            MetadataSyncStatus: syncStatus,
            AnglerUserId: anglerUserId ?? OwnerUserId,
            RecordedByUserId: recordedByUserId ?? OwnerUserId,
            Weight: weight,
            Length: length,
            Method: method,
            BaitOrLure: baitOrLure);
    }
}
