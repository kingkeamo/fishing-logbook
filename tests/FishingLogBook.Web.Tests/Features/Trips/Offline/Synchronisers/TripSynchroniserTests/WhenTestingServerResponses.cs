using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripStoreTests;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Synchronisers.TripSynchroniserTests;

public class WhenTestingServerResponses : BaseTripSynchroniserTest
{
    private static HttpRequestException Conflict()
    {
        return new HttpRequestException("conflict", null, HttpStatusCode.Conflict);
    }

    private static TripDetailDto Detail(Guid ownerUserId, string status)
    {
        return new TripDetailDto(new TripViewDto(TripId, ownerUserId, status, StartedOn.AddMinutes(-30), StartedOn.AddHours(2)));
    }

    private static Task<TripModel?> ReadAsync(MemoryTripStore store)
    {
        return store.GetAsync(OwnerUserId, TripId, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNotMarkATripSynchronisedWhenTheServerReturnsNoTrip()
    {
        // Arrange
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns((TripDto?)null);
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        (await ReadAsync(store))!.SyncStatus.Should().NotBe(SyncStatus.Synchronised);
        await MockTripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Id == TripId),
            Arg.Any<CancellationToken>());
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Error,
            DiagnosticEventNames.TripSyncFailed,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRethrowAConflictWhenTheServerCannotShowTheTrip()
    {
        // Arrange
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns<Task<TripDto?>>(_ => throw Conflict());
        MockTripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns((TripDetailDto?)null);
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        (await ReadAsync(store))!.SyncStatus.Should().NotBe(SyncStatus.Synchronised);
        await MockTripClient.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
        await MockTripClient.Received(1).UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRethrowAConflictWhenTheServerTripBelongsToSomeoneElse()
    {
        // Arrange
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns<Task<TripDto?>>(_ => throw Conflict());
        MockTripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Detail(OtherUserId, TripConstants.Completed));
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        (await ReadAsync(store))!.SyncStatus.Should().NotBe(SyncStatus.Synchronised);
        await MockTripClient.Received(1).UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRethrowAConflictWhenTheServerTripIsStillPlanned()
    {
        // Arrange
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns<Task<TripDto?>>(_ => throw Conflict());
        MockTripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Detail(OwnerUserId, TripConstants.Planned));
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        (await ReadAsync(store))!.SyncStatus.Should().NotBe(SyncStatus.Synchronised);
        await MockTripClient.Received(1).UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldResendTheTripWithTheServersLifecycleAfterAConflict()
    {
        // Arrange
        var upserts = 0;
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            upserts++;
            return upserts == 1
                ? throw Conflict()
                : Task.FromResult<TripDto?>(call.ArgAt<TripDto>(0));
        });
        MockTripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Detail(OwnerUserId, TripConstants.Completed));
        var store = await CreateStoreAsync(CreateTrip());
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockTripClient.Received(2).UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        await MockTripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Status == TripConstants.Completed
                && trip.StartedOn == StartedOn.AddMinutes(-30)
                && trip.EndedOn == StartedOn.AddHours(2)),
            Arg.Any<CancellationToken>());
        var saved = await ReadAsync(store);
        saved!.SyncStatus.Should().Be(SyncStatus.Synchronised);
        saved.Status.Should().Be(TripConstants.Completed);
    }

    [Fact]
    public async Task ItShouldKeepATripPendingWhenItsLocationMovedWhileSynchronising()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateTrip(location: CreateLocation()));
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var current = (await ReadAsync(store))!;
            await store.SaveAsync(
                current with { Location = current.Location! with { Longitude = current.Location.Longitude + 1 } },
                CancellationToken.None);
            return call.ArgAt<TripDto>(0);
        });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        (await ReadAsync(store))!.SyncStatus.Should().NotBe(SyncStatus.Synchronised);
        await MockTripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Location != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepATripPendingWhenItsLocationWasRemovedWhileSynchronising()
    {
        // Arrange
        var store = await CreateStoreAsync(CreateTrip(location: CreateLocation()));
        MockTripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var current = (await ReadAsync(store))!;
            await store.SaveAsync(current with { Location = null }, CancellationToken.None);
            return call.ArgAt<TripDto>(0);
        });
        var sut = CreateSut(store);

        // Act
        await sut.SynchronisePendingAsync(OwnerUserId, CancellationToken.None);

        // Assert
        (await ReadAsync(store))!.SyncStatus.Should().NotBe(SyncStatus.Synchronised);
        await MockTripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Location != null),
            Arg.Any<CancellationToken>());
    }
}
