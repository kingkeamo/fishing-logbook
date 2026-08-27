using AwesomeAssertions;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

public class WhenTestingGetSummariesByOwnerUserId : BaseTripRepositoryTest
{
    public WhenTestingGetSummariesByOwnerUserId(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheAnglerHasNoTrips()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();

        // Act
        var result = await Sut.GetSummariesByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherAnglersTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var owned = NewTrip(ownerUserId, placeName: "Lough Corrib");
        var foreign = NewTrip(otherUserId, placeName: "Lough Mask");
        await Sut.UpsertAsync(owned, CancellationToken.None);
        await Sut.UpsertAsync(foreign, CancellationToken.None);

        // Act
        var result = await Sut.GetSummariesByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(owned.Id);
        result.Value[0].PlaceName.Should().Be("Lough Corrib");
    }

    [Fact]
    public async Task ItShouldReturnNewestTripsFirst()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var older = NewTrip(
            ownerUserId,
            status: TripStatusEnum.Completed,
            startedOn: StartedOn.AddDays(-2),
            endedOn: StartedOn.AddDays(-2).AddHours(4));
        var newer = NewTrip(
            ownerUserId,
            status: TripStatusEnum.Completed,
            startedOn: StartedOn,
            endedOn: StartedOn.AddHours(4));
        await Sut.UpsertAsync(older, CancellationToken.None);
        await Sut.UpsertAsync(newer, CancellationToken.None);

        // Act
        var result = await Sut.GetSummariesByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.Value.Should().HaveCount(2);
        result.Value[0].Id.Should().Be(newer.Id);
        result.Value[1].Id.Should().Be(older.Id);
    }

    [Fact]
    public async Task ItShouldReturnActiveAndCompletedTrips()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var completed = NewTrip(
            ownerUserId,
            status: TripStatusEnum.Completed,
            startedOn: StartedOn.AddDays(-1),
            endedOn: StartedOn.AddDays(-1).AddHours(3));
        var active = NewTrip(ownerUserId);
        await Sut.UpsertAsync(completed, CancellationToken.None);
        await Sut.UpsertAsync(active, CancellationToken.None);

        // Act
        var result = await Sut.GetSummariesByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.Value.Select(trip => trip.Status)
            .Should()
            .Contain([TripStatusEnum.Active, TripStatusEnum.Completed]);
    }
    [Fact]
    public async Task ItShouldCountNothingForATripWithNoCatchesPhotographsOrNotes()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId);
        await Sut.UpsertAsync(trip, CancellationToken.None);

        // Act
        var result = await Sut.GetSummariesByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        var summary = result.Value.Single();
        summary.CatchCount.Should().Be(0);
        summary.PhotographCount.Should().Be(0);
        summary.NoteCount.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldCountTheCatchesPhotographsAndNotesOfEachTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var busy = NewTrip(ownerUserId, status: TripStatusEnum.Completed, endedOn: StartedOn.AddHours(4));
        var quiet = NewTrip(ownerUserId, startedOn: StartedOn.AddDays(1));
        await Sut.UpsertAsync(busy, CancellationToken.None);
        await Sut.UpsertAsync(quiet, CancellationToken.None);
        await AddCatchAsync(ownerUserId, busy.Id, "Pike");
        await AddCatchAsync(ownerUserId, busy.Id, "Brown Trout");
        await AddNoteAsync(busy.Id, ownerUserId);
        await AddPhotographAsync(busy.Id);
        await AddPhotographAsync(busy.Id);
        await AddPhotographAsync(busy.Id);

        // Act
        var result = await Sut.GetSummariesByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        var busySummary = result.Value.Single(summary => summary.Id == busy.Id);
        busySummary.CatchCount.Should().Be(2);
        busySummary.NoteCount.Should().Be(1);
        busySummary.PhotographCount.Should().Be(3);
        var quietSummary = result.Value.Single(summary => summary.Id == quiet.Id);
        quietSummary.CatchCount.Should().Be(0);
        quietSummary.NoteCount.Should().Be(0);
        quietSummary.PhotographCount.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotCountAnotherAnglersCatchesAgainstThisTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var owned = NewTrip(ownerUserId);
        var foreign = NewTrip(otherUserId);
        await Sut.UpsertAsync(owned, CancellationToken.None);
        await Sut.UpsertAsync(foreign, CancellationToken.None);
        await AddCatchAsync(ownerUserId, owned.Id);
        await AddCatchAsync(otherUserId, foreign.Id);

        // Act
        var result = await Sut.GetSummariesByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(owned.Id);
        result.Value[0].CatchCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldReturnTheTitleAndPlaceSnapshotOfEachTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId, title: "Morning session", placeName: "Lough Corrib");
        await Sut.UpsertAsync(trip, CancellationToken.None);

        // Act
        var result = await Sut.GetSummariesByOwnerUserIdAsync(ownerUserId, CancellationToken.None);

        // Assert
        var summary = result.Value.Single();
        summary.Title.Should().Be("Morning session");
        summary.PlaceName.Should().Be("Lough Corrib");
        summary.Status.Should().Be(TripStatusEnum.Active);
        summary.StartedOn.Should().Be(StartedOn);
        summary.EndedOn.Should().BeNull();
    }
}
