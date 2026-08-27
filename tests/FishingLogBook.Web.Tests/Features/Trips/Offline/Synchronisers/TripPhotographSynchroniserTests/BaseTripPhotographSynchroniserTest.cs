using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Offline.Dependencies;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Synchronisers;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripPhotographStoreTests;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripPhotographSynchroniserTests;

public class BaseTripPhotographSynchroniserTest
{
    protected static readonly Guid OwnerUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid OtherTripId =
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    protected static readonly Guid PhotographId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly Guid SecondPhotographId =
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    protected static readonly DateTimeOffset AddedOn =
        DateTimeOffset.Parse("2026-08-17T09:00:00Z");

    protected readonly ITripClient MockTripClient = Substitute.For<ITripClient>();
    protected readonly ITripDependencyService MockTripDependency =
        Substitute.For<ITripDependencyService>();
    protected readonly INetworkService MockNetworkService = Substitute.For<INetworkService>();
    protected readonly IDiagnosticLogger MockDiagnostics = Substitute.For<IDiagnosticLogger>();
    protected readonly ILoggingService MockLogging = Substitute.For<ILoggingService>();

    protected BaseTripPhotographSynchroniserTest()
    {
        MockNetworkService.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(true);
        MockTripDependency.IsTripReadyForServerAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        MockTripClient.CreatePhotographUploadAsync(
                Arg.Any<Guid>(),
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var tripId = call.ArgAt<Guid>(0);
                var request = call.ArgAt<PhotographUploadRequestDto>(1);
                return new PhotographUploadDto(
                    $"trips/{OwnerUserId:D}/{tripId:D}/{request.PhotographId:D}",
                    $"https://storage.test/{request.PhotographId:D}");
            });
    }

    protected TripPhotographSynchroniser CreateSut(MemoryTripPhotographStore store)
    {
        return new TripPhotographSynchroniser(
            store,
            MockTripDependency,
            MockTripClient,
            MockNetworkService,
            MockDiagnostics,
            MockLogging);
    }

    protected static TripPhotographModel CreatePhotograph(
        Guid? photographId = null,
        Guid? tripId = null,
        Guid? ownerUserId = null,
        SyncStatus syncStatus = SyncStatus.SavedLocally,
        DateTimeOffset? capturedOn = null)
    {
        return new TripPhotographModel(
            photographId ?? PhotographId,
            tripId ?? TripId,
            ownerUserId ?? OwnerUserId,
            "image/jpeg",
            AddedOn,
            capturedOn,
            [1, 2, 3],
            SyncStatus: syncStatus);
    }

    protected static async Task<MemoryTripPhotographStore> CreateStoreAsync(
        params TripPhotographModel[] photographs)
    {
        var store = new MemoryTripPhotographStore();
        foreach (var photograph in photographs)
        {
            await store.SaveAsync(photograph, CancellationToken.None);
        }

        return store;
    }
}
