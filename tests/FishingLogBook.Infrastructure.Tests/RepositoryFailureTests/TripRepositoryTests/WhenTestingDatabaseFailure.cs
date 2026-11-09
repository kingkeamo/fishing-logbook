using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.TripRepositoryTests;

public class WhenTestingDatabaseFailure
{
    private readonly RecordingLogger<TripRepository> _logger = new();
    private readonly TripRepository _sut;

    public WhenTestingDatabaseFailure()
    {
        _sut = new TripRepository(FailingConnectionFactory.Create(), _logger, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingATrip()
    {
        // Arrange
        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingTripSummaries()
    {
        // Arrange
        // Act
        var result = await _sut.GetSummariesForUserAsync(
            new GetMyTripsArgs { UserId = Guid.NewGuid() },
            CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingTheCatchesOfATrip()
    {
        // Arrange
        // Act
        var result = await _sut.GetCatchSummariesByTripIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenSavingATrip()
    {
        // Arrange
        var trip = new Trip { Id = Guid.NewGuid(), OwnerUserId = Guid.NewGuid() };

        // Act
        var result = await _sut.UpsertAsync(trip, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenActivatingATrip()
    {
        // Arrange
        var trip = new Trip { Id = Guid.NewGuid(), OwnerUserId = Guid.NewGuid() };

        // Act
        var result = await _sut.ActivateAsync(trip, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenFindingPlannedTrips()
    {
        // Arrange
        var args = new FindPlannedTripsArgs
        {
            UserId = Guid.NewGuid(),
            From = DateTimeOffset.Parse("2026-08-17T00:00:00Z"),
            To = DateTimeOffset.Parse("2026-08-20T00:00:00Z")
        };

        // Act
        var result = await _sut.FindPlannedAsync(args, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }
}
