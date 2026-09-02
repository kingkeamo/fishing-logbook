using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripListTests;

public class BaseTripListTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid LocalTripId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid RemoteTripId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-27T06:00:00Z");

    protected static BunitContext CreateContext(
        ITripStore tripStore,
        ITripClient tripClient,
        ICatchStore? catchStore = null,
        ILoggingService? logging = null,
        ITripParticipantClient? participantClient = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(tripStore);
        context.Services.AddSingleton(tripClient);
        context.Services.AddSingleton(participantClient ?? QuietParticipantClient());
        context.Services.AddSingleton(catchStore ?? QuietCatchStore());
        context.Services.AddSingleton(SignedInOwner());
        context.Services.AddSingleton<ITimeService>(TestTimeService.WithOffset(TimeSpan.Zero));
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        var authorization = context.AddAuthorization();
        authorization.SetAuthorized("tester@example.test");
        return context;
    }

    protected static ITripParticipantClient QuietParticipantClient()
    {
        var client = Substitute.For<ITripParticipantClient>();
        client.GetMyInvitationsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TripInvitationDto>>([]));
        return client;
    }

    protected static ITripStore StoreWith(params TripModel[] trips)
    {
        var store = Substitute.For<ITripStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(trips);
        return store;
    }

    protected static ITripClient ClientWith(params TripSummaryDto[] summaries)
    {
        var client = Substitute.For<ITripClient>();
        client.GetMyAsync(Arg.Any<CancellationToken>()).Returns(summaries);
        return client;
    }

    protected static ICatchStore QuietCatchStore(params CatchModel[] catches)
    {
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(catches);
        return store;
    }

    protected static ILocalCatchOwnerService SignedInOwner()
    {
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerUserId);
        return owner;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static CatchModel Catch(Guid? tripId)
    {
        var catchId = Guid.NewGuid();
        return new CatchModel(
            catchId,
            StartedOn.AddMinutes(30),
            [],
            CaughtByUserId: OwnerUserId,
            TripId: tripId);
    }

    protected static TripModel LocalTrip(
        Guid? tripId = null,
        string status = TripConstants.Active,
        DateTimeOffset? startedOn = null,
        string? title = null,
        string? placeName = null,
        IReadOnlyList<TripPhotographModel>? photographs = null,
        IReadOnlyList<TripNoteModel>? notes = null)
    {
        return new TripModel(
            tripId ?? LocalTripId,
            OwnerUserId,
            status,
            startedOn ?? StartedOn,
            status == TripConstants.Completed ? (startedOn ?? StartedOn).AddHours(3) : null,
            Title: title,
            PlaceName: placeName,
            Photographs: photographs,
            Notes: notes);
    }

    protected static TripSummaryDto RemoteTrip(
        Guid? tripId = null,
        string status = TripConstants.Completed,
        DateTimeOffset? startedOn = null,
        string? placeName = null,
        int catchCount = 0,
        int photographCount = 0,
        int noteCount = 0)
    {
        var started = startedOn ?? StartedOn.AddDays(-2);
        return new TripSummaryDto(
            tripId ?? RemoteTripId,
            status,
            started,
            status == TripConstants.Completed ? started.AddHours(4) : null)
        {
            PlaceName = placeName,
            CatchCount = catchCount,
            PhotographCount = photographCount,
            NoteCount = noteCount
        };
    }
}
