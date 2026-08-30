using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingCorrectAngler : BaseCatchRepositoryTest
{
    public WhenTestingCorrectAngler(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldFailWhenTheCatchIsMissing()
    {
        // Arrange
        var args = new PersistCatchAnglerArgs
        {
            CatchId = Guid.NewGuid(),
            AnglerUserId = Guid.NewGuid()
        };

        // Act
        var result = await Sut.CorrectAnglerAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to correct the catch angler.");
    }

    [Fact]
    public async Task ItShouldFailWhenTheCatchIsNotAttachedToATrip()
    {
        // Arrange
        var recorderUserId = await CreateUserAsync();
        var correctedAnglerUserId = await CreateUserAsync();
        var catchRecord = NewCatch(recorderUserId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var args = new PersistCatchAnglerArgs
        {
            CatchId = catchRecord.Id,
            AnglerUserId = correctedAnglerUserId
        };

        // Act
        var result = await Sut.CorrectAnglerAsync(args, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        loaded.Value!.UserId.Should().Be(recorderUserId);
        loaded.Value.AnglerUserId.Should().Be(recorderUserId);
    }

    [Fact]
    public async Task ItShouldUpdateUserIdAndAnglerUserIdWithoutChangingRecordedByUserId()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var recorderUserId = await CreateUserAsync();
        var correctedAnglerUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);
        await AddParticipantAsync(tripId, recorderUserId, ownerUserId);
        await AddParticipantAsync(tripId, correctedAnglerUserId, ownerUserId);
        var catchRecord = NewCatch(recorderUserId, recorderUserId, tripId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var args = new PersistCatchAnglerArgs
        {
            CatchId = catchRecord.Id,
            AnglerUserId = correctedAnglerUserId
        };

        // Act
        var result = await Sut.CorrectAnglerAsync(args, CancellationToken.None);
        var loaded = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        loaded.Value.Should().NotBeNull();
        loaded.Value!.UserId.Should().Be(correctedAnglerUserId);
        loaded.Value.AnglerUserId.Should().Be(correctedAnglerUserId);
        loaded.Value.RecordedByUserId.Should().Be(recorderUserId);
        loaded.Value.TripId.Should().Be(tripId);
    }

    [Fact]
    public async Task ItShouldMakeTheCorrectionVisibleThroughGetDetailForUser()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var recorderUserId = await CreateUserAsync();
        var correctedAnglerUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);
        await AddParticipantAsync(tripId, recorderUserId, ownerUserId);
        await AddParticipantAsync(tripId, correctedAnglerUserId, ownerUserId);
        await CreateProfileAsync(correctedAnglerUserId, "Patrick Connolly");
        await CreateProfileAsync(recorderUserId, "Myles Costello");
        var catchRecord = NewCatch(recorderUserId, recorderUserId, tripId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        var args = new PersistCatchAnglerArgs
        {
            CatchId = catchRecord.Id,
            AnglerUserId = correctedAnglerUserId
        };

        // Act
        await Sut.CorrectAnglerAsync(args, CancellationToken.None);
        var detail = await Sut.GetDetailForUserAsync(catchRecord.Id, correctedAnglerUserId, CancellationToken.None);

        // Assert
        detail.IsSuccess.Should().BeTrue();
        detail.Value.Should().NotBeNull();
        detail.Value!.Catch.AnglerUserId.Should().Be(correctedAnglerUserId);
        detail.Value.Catch.RecordedByUserId.Should().Be(recorderUserId);
        detail.Value.AnglerName.Should().Be("Patrick Connolly");
        detail.Value.RecordedByName.Should().Be("Myles Costello");
    }
}
