using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Offline.Dependencies;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Synchronisers;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripNoteSynchroniserTests;

public class BaseTripNoteSynchroniserTest
{
    protected static readonly Guid OwnerUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid OtherTripId =
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    protected static readonly Guid NoteId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly Guid SecondNoteId =
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    protected static readonly DateTimeOffset RecordedOn =
        DateTimeOffset.Parse("2026-08-17T09:12:00Z");

    protected readonly ITripClient MockTripClient = Substitute.For<ITripClient>();
    protected readonly ITripDependencyService MockTripDependency =
        Substitute.For<ITripDependencyService>();
    protected readonly INetworkService MockNetworkService = Substitute.For<INetworkService>();
    protected readonly IDiagnosticLogger MockDiagnostics = Substitute.For<IDiagnosticLogger>();
    protected readonly ILoggingService MockLogging = Substitute.For<ILoggingService>();

    protected BaseTripNoteSynchroniserTest()
    {
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        MockTripDependency.IsTripReadyForServerAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        MockTripClient.RecordNoteAsync(
                Arg.Any<Guid>(),
                Arg.Any<RecordTripNoteDto>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var tripId = call.ArgAt<Guid>(0);
                var request = call.ArgAt<RecordTripNoteDto>(1);
                return new TripNoteDto(request.NoteId, tripId, request.Text, request.RecordedOn);
            });
    }

    protected TripNoteSynchroniser CreateSut(MemoryTripNoteStore store)
    {
        return new TripNoteSynchroniser(
            store,
            MockTripDependency,
            MockTripClient,
            MockNetworkService,
            MockDiagnostics,
            MockLogging);
    }

    protected static TripNoteModel CreateNote(
        Guid? noteId = null,
        Guid? tripId = null,
        Guid? ownerUserId = null,
        string text = "water dropped about a foot",
        SyncStatus syncStatus = SyncStatus.SavedLocally,
        DateTimeOffset? recordedOn = null)
    {
        return new TripNoteModel(
            noteId ?? NoteId,
            tripId ?? TripId,
            ownerUserId ?? OwnerUserId,
            text,
            recordedOn ?? RecordedOn,
            syncStatus);
    }

    protected static async Task<MemoryTripNoteStore> CreateStoreAsync(params TripNoteModel[] notes)
    {
        var store = new MemoryTripNoteStore();
        foreach (var note in notes)
        {
            await store.SaveAsync(note, CancellationToken.None);
        }

        return store;
    }
}
