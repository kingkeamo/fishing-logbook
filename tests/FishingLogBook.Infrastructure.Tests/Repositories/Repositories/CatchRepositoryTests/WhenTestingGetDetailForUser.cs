using AwesomeAssertions;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetDetailForUser : BaseCatchRepositoryTest
{
    public WhenTestingGetDetailForUser(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnNullWhenTheCatchDoesNotExist()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.GetDetailForUserAsync(Guid.NewGuid(), userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldAllowTheAnglerToReadTheirOwnCatch()
    {
        // Arrange
        var CaughtByUserId = await CreateUserAsync();
        var catchRecord = NewCatch(CaughtByUserId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetDetailForUserAsync(catchRecord.Id, CaughtByUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Catch.Id.Should().Be(catchRecord.Id);
    }

    [Fact]
    public async Task ItShouldAllowTheRecorderToReadACatchTheyRecordedForAnotherAngler()
    {
        // Arrange
        var CaughtByUserId = await CreateUserAsync();
        var recorderUserId = await CreateUserAsync();
        var catchRecord = NewCatch(CaughtByUserId, recorderUserId, tripId: null);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetDetailForUserAsync(catchRecord.Id, recorderUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Catch.Id.Should().Be(catchRecord.Id);
    }

    [Fact]
    public async Task ItShouldAllowTheTripOwnerToReadAParticipantsCatchOnTheirTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var CaughtByUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);
        await AddParticipantAsync(tripId, CaughtByUserId, ownerUserId);
        var catchRecord = NewCatch(CaughtByUserId, CaughtByUserId, tripId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetDetailForUserAsync(catchRecord.Id, ownerUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Catch.Id.Should().Be(catchRecord.Id);
    }

    [Fact]
    public async Task ItShouldAllowAnAcceptedParticipantToReadAnotherParticipantsCatchOnTheSharedTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var CaughtByUserId = await CreateUserAsync();
        var viewerUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);
        await AddParticipantAsync(tripId, CaughtByUserId, ownerUserId);
        await AddParticipantAsync(tripId, viewerUserId, ownerUserId);
        var catchRecord = NewCatch(CaughtByUserId, CaughtByUserId, tripId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetDetailForUserAsync(catchRecord.Id, viewerUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Catch.Id.Should().Be(catchRecord.Id);
    }

    [Fact]
    public async Task ItShouldRejectAPendingParticipant()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var CaughtByUserId = await CreateUserAsync();
        var pendingUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);
        await AddParticipantAsync(tripId, CaughtByUserId, ownerUserId);
        await AddParticipantAsync(tripId, pendingUserId, ownerUserId, TripParticipantStatusEnum.Pending);
        var catchRecord = NewCatch(CaughtByUserId, CaughtByUserId, tripId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetDetailForUserAsync(catchRecord.Id, pendingUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRejectARemovedParticipant()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var CaughtByUserId = await CreateUserAsync();
        var removedUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);
        await AddParticipantAsync(tripId, CaughtByUserId, ownerUserId);
        await AddParticipantAsync(
            tripId,
            removedUserId,
            ownerUserId,
            removedOn: TripStartedOn.AddHours(2));
        var catchRecord = NewCatch(CaughtByUserId, CaughtByUserId, tripId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetDetailForUserAsync(catchRecord.Id, removedUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRejectAnUnrelatedUserOutsideAnySharedTrip()
    {
        // Arrange
        var CaughtByUserId = await CreateUserAsync();
        var unrelatedUserId = await CreateUserAsync();
        var catchRecord = NewCatch(CaughtByUserId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetDetailForUserAsync(catchRecord.Id, unrelatedUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldProjectAnglerAndRecorderNamesOnADetailRead()
    {
        // Arrange
        var CaughtByUserId = await CreateUserAsync();
        var recorderUserId = await CreateUserAsync();
        await CreateProfileAsync(CaughtByUserId, "Patrick Connolly");
        await CreateProfileAsync(recorderUserId, "Myles Costello");
        var catchRecord = NewCatch(CaughtByUserId, recorderUserId, tripId: null);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetDetailForUserAsync(catchRecord.Id, CaughtByUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AnglerName.Should().Be("Patrick Connolly");
        result.Value.RecordedByName.Should().Be("Myles Costello");
    }
}
