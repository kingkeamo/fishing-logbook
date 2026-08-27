using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetCatchSummariesByTripId : BaseTripRepositoryTest
{
    public WhenTestingGetCatchSummariesByTripId(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnNothingForAnUnknownTrip()
    {
        // Arrange
        // Act
        var result = await Sut.GetCatchSummariesByTripIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnNothingForATripWithNoCatches()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId);
        await Sut.UpsertAsync(trip, CancellationToken.None);

        // Act
        var result = await Sut.GetCatchSummariesByTripIdAsync(trip.Id, CancellationToken.None);

        // Assert
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotReturnACatchFromAnotherTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId);
        var otherTrip = NewTrip(ownerUserId, startedOn: StartedOn.AddDays(1));
        await Sut.UpsertAsync(otherTrip, CancellationToken.None);
        await Sut.UpsertAsync(trip, CancellationToken.None);
        var owned = await AddCatchAsync(ownerUserId, trip.Id, "Pike");
        await AddCatchAsync(ownerUserId, otherTrip.Id, "Brown Trout");

        // Act
        var result = await Sut.GetCatchSummariesByTripIdAsync(trip.Id, CancellationToken.None);

        // Assert
        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(owned);
        result.Value[0].SpeciesName.Should().Be("Pike");
    }

    [Fact]
    public async Task ItShouldReturnTheCatchesOfTheTripOldestFirst()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var trip = NewTrip(ownerUserId);
        await Sut.UpsertAsync(trip, CancellationToken.None);
        await AddCatchAsync(ownerUserId, trip.Id, "Pike");
        await AddCatchAsync(ownerUserId, trip.Id, null);

        // Act
        var result = await Sut.GetCatchSummariesByTripIdAsync(trip.Id, CancellationToken.None);

        // Assert
        result.Value.Should().HaveCount(2);
        result.Value.Should().BeInAscendingOrder(summary => summary.CaughtOn);
        result.Value.Select(summary => summary.SpeciesName).Should().Contain([null, "Pike"]);
    }
}
