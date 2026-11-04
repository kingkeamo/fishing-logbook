using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.DashboardRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGet(PostgresFixture fixture) : BaseCatchRepositoryTest(fixture)
{
    private DashboardRepository CreateDashboard()
    {
        return new DashboardRepository(ConnectionFactory, NullLogger<DashboardRepository>.Instance);
    }

    [Fact]
    public async Task ItShouldReturnTruthfulZerosAndEmptySectionsForAnEmptyAccount()
    {
        // Arrange
        var owner = await CreateUserAsync();

        // Act
        var result = await CreateDashboard().GetAsync(new GetDashboardArgs { UserId = owner }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Lifetime.Catches.Should().Be(0);
        result.Value.Lifetime.Trips.Should().Be(0);
        result.Value.Lifetime.Species.Should().Be(0);
        result.Value.AllTimePersonalBests.Should().BeEmpty();
        result.Value.CatchStatistics.Should().BeEmpty();
        result.Value.FilteredPersonalBests.Should().BeEmpty();
        result.Value.Species.Should().BeEmpty();
        result.Value.Methods.Should().BeEmpty();
        result.Value.Venues.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepLifetimeTotalsAndBestsIndependentOfEveryExploreFilter()
    {
        // Arrange
        var owner = await CreateUserAsync();
        var other = await CreateUserAsync();
        var start = DateTimeOffset.Parse("2026-11-01T00:00:00Z");
        var allTimeWinner = await SaveAsync(owner, start.AddYears(-2), 50);
        var filteredWinner = await SaveAsync(owner, start, 8);
        await SaveAsync(owner, start.AddDays(1), null);
        await SaveAsync(owner, start, 20, species: "Perch");
        await SaveAsync(owner, start, 30, method: "Fly");
        await SaveAsync(owner, start, 40, venue: "Another lake");
        await SaveAsync(other, start, 100);
        var sut = CreateDashboard();

        // Act
        var result = await sut.GetAsync(new GetDashboardArgs
        {
            UserId = owner,
            FromInclusive = start,
            ToExclusive = start.AddDays(2),
            Species = "Pike",
            Method = "Lure",
            Venue = "Corrib"
        }, CancellationToken.None);
        var noMatches = await sut.GetAsync(new GetDashboardArgs { UserId = owner, Venue = "' OR true --" }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Lifetime.Catches.Should().Be(6);
        result.Value.Lifetime.Species.Should().Be(2);
        result.Value.CatchStatistics.Should().ContainSingle(row =>
            row.SpeciesName == "Pike" && row.Method == "Lure" && row.Count == 2);
        result.Value.FilteredPersonalBests.Should().ContainSingle(row => row.CatchId == filteredWinner && row.Weight == 8);
        result.Value.AllTimePersonalBests.Should().Contain(row => row.CatchId == allTimeWinner && row.Weight == 50);
        result.Value.AllTimePersonalBests.Should().NotContain(row => row.Weight == 100);
        result.Value.Species.Should().Equal("Perch", "Pike");
        result.Value.Methods.Should().Equal("Fly", "Lure");
        result.Value.Venues.Should().Equal("Another lake", "Corrib");
        noMatches.Value.Lifetime.Catches.Should().Be(6);
        noMatches.Value.AllTimePersonalBests.Should().BeEquivalentTo(result.Value.AllTimePersonalBests);
        noMatches.Value.CatchStatistics.Should().BeEmpty();
        noMatches.Value.FilteredPersonalBests.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldFilterByCatchInstantsIncludingBothLocalCalendarDateBoundaries()
    {
        // Arrange
        var owner = await CreateUserAsync();
        var start = DateTimeOffset.Parse("2026-03-08T05:00:00Z");
        var end = DateTimeOffset.Parse("2026-03-09T04:00:00Z");
        await SaveAsync(owner, start.AddTicks(-10), 50);
        var first = await SaveAsync(owner, start, 2);
        var last = await SaveAsync(owner, end.AddTicks(-10), 3);
        await SaveAsync(owner, end, 60);

        // Act
        var result = await CreateDashboard().GetAsync(new GetDashboardArgs
        {
            UserId = owner,
            FromInclusive = start,
            ToExclusive = end
        }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Lifetime.Catches.Should().Be(4);
        result.Value.CatchStatistics.Should().ContainSingle(row => row.Count == 2);
        result.Value.FilteredPersonalBests.Should().ContainSingle(row => row.CatchId == last);
        result.Value.FilteredPersonalBests.Should().NotContain(row => row.CatchId == first);
        result.Value.AllTimePersonalBests.Should().ContainSingle(row => row.Weight == 60);
    }

    [Fact]
    public async Task ItShouldCountWeightlessAndUnclassifiedCatchesWithoutInventingPersonalBests()
    {
        // Arrange
        var owner = await CreateUserAsync();
        var date = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        await SaveAsync(owner, date, null, species: null, method: null, venue: null);
        await SaveAsync(owner, date, null, species: " ", method: "", venue: null);
        await SaveAsync(owner, date, null);

        // Act
        var result = await CreateDashboard().GetAsync(new GetDashboardArgs { UserId = owner }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Lifetime.Catches.Should().Be(3);
        result.Value.Lifetime.Species.Should().Be(1);
        result.Value.CatchStatistics.Should().HaveCount(2);
        result.Value.CatchStatistics.Should().Contain(row => row.SpeciesName == null && row.Method == null && row.Count == 2);
        result.Value.AllTimePersonalBests.Should().BeEmpty();
        result.Value.FilteredPersonalBests.Should().BeEmpty();
        result.Value.Species.Should().Equal("Pike");
        result.Value.Methods.Should().Equal("Lure");
        result.Value.Venues.Should().Equal("Corrib");
    }

    [Fact]
    public async Task ItShouldResolveEqualWeightsByEarliestCatchThenLowestIdInBothTables()
    {
        // Arrange
        var owner = await CreateUserAsync();
        var date = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        await SaveAsync(owner, date.AddDays(1), 10);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() }.OrderBy(value => value.ToString("N"), StringComparer.Ordinal).ToArray();
        var lowerId = ids[0];
        var higherId = ids[1];
        await SaveAsync(owner, date, 10, id: higherId);
        await SaveAsync(owner, date, 10, id: lowerId);

        // Act
        var result = await CreateDashboard().GetAsync(new GetDashboardArgs { UserId = owner }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AllTimePersonalBests.Should().ContainSingle(row => row.CatchId == lowerId && row.Weight == 10);
        result.Value.FilteredPersonalBests.Should().ContainSingle(row => row.CatchId == lowerId);
        result.Value.CatchStatistics.Should().ContainSingle(row => row.Count == 3);
    }

    [Fact]
    public async Task ItShouldCountActualOwnedAndAcceptedSharedTripsIndependentlyOfCatches()
    {
        // Arrange
        var owner = await CreateUserAsync();
        var other = await CreateUserAsync();
        await CreateTripAsync(owner);
        var shared = await CreateTripAsync(other);
        await AddParticipantAsync(shared, owner, other);
        var removed = await CreateTripAsync(other);
        await AddParticipantAsync(removed, owner, other, removedOn: TripStartedOn);
        await Trips.UpsertAsync(new Trip
        {
            Id = Guid.NewGuid(),
            OwnerUserId = owner,
            Status = TripStatusEnum.Planned,
            StartedOn = TripStartedOn,
            PlannedStartOn = TripStartedOn
        }, CancellationToken.None);

        // Act
        var result = await CreateDashboard().GetAsync(new GetDashboardArgs { UserId = owner }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Lifetime.Trips.Should().Be(2);
        result.Value.Lifetime.Catches.Should().Be(0);
        result.Value.AllTimePersonalBests.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldAttributeSharedCatchesToTheAnglerRatherThanTheRecorder()
    {
        // Arrange
        var owner = await CreateUserAsync();
        var other = await CreateUserAsync();
        var trip = await CreateTripAsync(owner);
        await AddParticipantAsync(trip, other, owner);
        var recordedForOther = NewCatch(other, owner, trip);
        var recordedByOther = NewCatch(owner, other, trip);
        (await Sut.UpsertAsync(recordedForOther, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await Sut.UpsertAsync(recordedByOther, CancellationToken.None)).IsSuccess.Should().BeTrue();

        // Act
        var result = await CreateDashboard().GetAsync(new GetDashboardArgs { UserId = owner }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Lifetime.Catches.Should().Be(1);
        result.Value.CatchStatistics.Should().ContainSingle(row => row.Count == 1);
    }

    [Fact]
    public async Task ItShouldKeepIndependentCountsAndWinnersForEverySpeciesAndMethodPair()
    {
        // Arrange
        var owner = await CreateUserAsync();
        var date = DateTimeOffset.Parse("2026-01-01T00:00Z");
        var lure = await SaveAsync(owner, date, 12);
        await SaveAsync(owner, date, 2);
        var fly = await SaveAsync(owner, date, 5, method: "Fly");
        await SaveAsync(owner, date, null, method: "Fly");
        var perch = await SaveAsync(owner, date, 3, species: "Perch");
        var unspecified = await SaveAsync(owner, date, 4, species: null, method: null);

        // Act
        var result = await CreateDashboard().GetAsync(new GetDashboardArgs { UserId = owner }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CatchStatistics.Should().HaveCount(4);
        result.Value.CatchStatistics.Should().Contain(row => row.SpeciesName == "Pike" && row.Method == "Lure" && row.Count == 2);
        result.Value.CatchStatistics.Should().Contain(row => row.SpeciesName == "Pike" && row.Method == "Fly" && row.Count == 2);
        result.Value.CatchStatistics.Should().Contain(row => row.SpeciesName == "Perch" && row.Method == "Lure" && row.Count == 1);
        result.Value.CatchStatistics.Should().Contain(row => row.SpeciesName == null && row.Method == null && row.Count == 1);
        result.Value.AllTimePersonalBests.Select(row => row.CatchId).Should().BeEquivalentTo([lure, fly, perch, unspecified]);
        result.Value.FilteredPersonalBests.Should().BeEquivalentTo(result.Value.AllTimePersonalBests);
        result.Value.Lifetime.Species.Should().Be(2);
        result.Value.Lifetime.Catches.Should().Be(6);
    }

    private async Task<Guid> SaveAsync(
        Guid owner, DateTimeOffset caughtOn, decimal? weight,
        string? species = "Pike", string? method = "Lure", string? venue = "Corrib", Guid? id = null)
    {
        var identity = id ?? Guid.NewGuid();
        var record = new Catch
        {
            Id = identity,
            CaughtByUserId = owner,
            RecordedByUserId = owner,
            CaughtOn = caughtOn,
            Weight = weight,
            SpeciesName = species,
            Method = method,
            PlaceName = venue,
            Location = venue is null ? null : SampleLocation(),
            Photographs = [new CatchPhotograph { Id = Guid.NewGuid(), CatchId = identity, ContentType = "image/jpeg" }]
        };
        var result = await Sut.UpsertAsync(record, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(string.Join("; ", Logger.Records.Select(log => log.Exception?.Message)));
        return record.Id;
    }
}
