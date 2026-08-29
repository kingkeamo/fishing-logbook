using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripStoreTests;

public class WhenTestingHydrate
{
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ParticipantUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    [Fact]
    public async Task ItShouldRefuseToCacheASharedTripForANonParticipant()
    {
        // Arrange
        var store = new MemoryTripStore();

        // Act
        var act = () => store.HydrateAsync(SharedTrip(), StrangerUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await store.GetAsync(StrangerUserId, TripId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldMakeTheSharedTripReadableByTheAcceptedParticipant()
    {
        // Arrange
        var store = new MemoryTripStore();

        // Act
        await store.HydrateAsync(SharedTrip(), ParticipantUserId, CancellationToken.None);

        // Assert
        var stored = await store.GetAsync(ParticipantUserId, TripId, CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.Id.Should().Be(TripId);
        stored.OwnerUserId.Should().Be(OwnerUserId);
        stored.Origin.Should().Be(TripOriginEnum.Server);
        stored.RoleFor(ParticipantUserId).Should().Be(TripAccessRoleEnum.Participant);
    }

    [Fact]
    public async Task ItShouldKeepTheSharedTripHiddenFromANonParticipant()
    {
        // Arrange
        var store = new MemoryTripStore();
        await store.HydrateAsync(SharedTrip(), ParticipantUserId, CancellationToken.None);

        // Act
        var stored = await store.GetAsync(StrangerUserId, TripId, CancellationToken.None);

        // Assert
        stored.Should().BeNull();
        (await store.GetAllAsync(StrangerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNeverTreatASharedTripAsTheParticipantsOwnActiveTrip()
    {
        // Arrange
        var store = new MemoryTripStore();

        // Act
        await store.HydrateAsync(SharedTrip(), ParticipantUserId, CancellationToken.None);

        // Assert
        (await store.GetActiveAsync(ParticipantUserId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNeverQueueASharedTripForTheTripUpsertOutbox()
    {
        // Arrange
        var store = new MemoryTripStore();

        // Act
        await store.HydrateAsync(
            SharedTrip() with { SyncStatus = SyncStatus.SavedLocally },
            ParticipantUserId,
            CancellationToken.None);

        // Assert
        (await store.GetPendingAsync(ParticipantUserId, CancellationToken.None)).Should().BeEmpty();
        (await store.GetPendingAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldStillQueueTheAnglersOwnLocallyCreatedTrip()
    {
        // Arrange
        var store = new MemoryTripStore();

        // Act
        await store.SaveAsync(OwnTrip(), CancellationToken.None);

        // Assert
        var pending = await store.GetPendingAsync(ParticipantUserId, CancellationToken.None);
        pending.Should().ContainSingle();
        pending[0].Origin.Should().Be(TripOriginEnum.Local);
    }

    [Fact]
    public async Task ItShouldListTheSharedTripForTheOwnerAndTheParticipantUnderTheSameId()
    {
        // Arrange
        var store = new MemoryTripStore();

        // Act
        await store.HydrateAsync(SharedTrip(), ParticipantUserId, CancellationToken.None);

        // Assert
        var forParticipant = await store.GetAllAsync(ParticipantUserId, CancellationToken.None);
        var forOwner = await store.GetAllAsync(OwnerUserId, CancellationToken.None);
        forParticipant.Should().ContainSingle();
        forOwner.Should().ContainSingle();
        forParticipant[0].Id.Should().Be(forOwner[0].Id);
    }

    private static TripModel SharedTrip()
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            TripConstants.Active,
            StartedOn,
            PlaceName: "Lough Corrib",
            SyncStatus: SyncStatus.Synchronised,
            SyncedAt: StartedOn,
            ParticipantUserIds: [ParticipantUserId],
            Origin: TripOriginEnum.Server);
    }

    private static TripModel OwnTrip()
    {
        return new TripModel(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ParticipantUserId,
            TripConstants.Active,
            StartedOn,
            SyncStatus: SyncStatus.SavedLocally,
            Origin: TripOriginEnum.Local);
    }
}
