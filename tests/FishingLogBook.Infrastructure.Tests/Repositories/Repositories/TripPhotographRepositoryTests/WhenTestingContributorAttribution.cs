using AwesomeAssertions;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripPhotographRepositoryTests;

public class WhenTestingContributorAttribution : BaseTripPhotographRepositoryTest
{
    public WhenTestingContributorAttribution(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldRefuseAPhotographWithNoContributor()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var tripId = (await CreateTripAsync(ownerUserId)).Id;

        // Act
        var result = await Sut.UpsertAsync(
            new TripPhotograph
            {
                Id = Guid.NewGuid(),
                TripId = tripId,
                ObjectKey = $"trips/{ownerUserId:D}/{tripId:D}/{Guid.NewGuid():D}",
                ContentType = PhotographContentTypeConstants.Jpeg,
                AddedOn = StartedOn.AddHours(1)
            },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the trip photograph.");
    }

    [Fact]
    public async Task ItShouldRefuseAContributorWhoIsNotAKnownAngler()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var tripId = (await CreateTripAsync(ownerUserId)).Id;

        // Act
        var result = await Sut.UpsertAsync(
            NewPhotograph(ownerUserId, tripId, contributedByUserId: Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the trip photograph.");
    }

    [Fact]
    public async Task ItShouldKeepEachContributorsAttributionOnTheSameTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var participantUserId = await CreateUserAsync();
        var tripId = (await CreateTripAsync(ownerUserId)).Id;
        var ownerPhotograph = NewPhotograph(ownerUserId, tripId);
        var participantPhotograph = NewPhotograph(
            participantUserId,
            tripId,
            addedOn: StartedOn.AddHours(2));

        // Act
        await Sut.UpsertAsync(ownerPhotograph, CancellationToken.None);
        await Sut.UpsertAsync(participantPhotograph, CancellationToken.None);

        // Assert
        var stored = await Sut.GetByTripIdAsync(tripId, CancellationToken.None);
        stored.Value.Should().HaveCount(2);
        stored.Value.Single(photograph => photograph.Id == ownerPhotograph.Id)
            .ContributedByUserId.Should().Be(ownerUserId);
        stored.Value.Single(photograph => photograph.Id == participantPhotograph.Id)
            .ContributedByUserId.Should().Be(participantUserId);
    }

    [Fact]
    public async Task ItShouldRoundTripTheContributorThroughASingleRead()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var participantUserId = await CreateUserAsync();
        var tripId = (await CreateTripAsync(ownerUserId)).Id;
        var photograph = NewPhotograph(participantUserId, tripId);

        // Act
        await Sut.UpsertAsync(photograph, CancellationToken.None);

        // Assert
        var stored = await Sut.GetByIdAsync(photograph.Id, CancellationToken.None);
        stored.Value!.ContributedByUserId.Should().Be(participantUserId);
        stored.Value.ObjectKey.Should().Contain(participantUserId.ToString("D"));
    }
}
