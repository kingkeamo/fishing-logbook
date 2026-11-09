using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.TripPhotographRepositoryTests;

public class WhenTestingDatabaseFailure
{
    private readonly RecordingLogger<TripPhotographRepository> _logger = new();
    private readonly TripPhotographRepository _sut;

    public WhenTestingDatabaseFailure()
    {
        _sut = new TripPhotographRepository(FailingConnectionFactory.Create(), _logger, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingAPhotograph()
    {
        // Arrange
        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingThePhotographsOfATrip()
    {
        // Arrange
        // Act
        var result = await _sut.GetByTripIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenSavingAPhotograph()
    {
        // Arrange
        var photograph = new TripPhotograph { Id = Guid.NewGuid(), TripId = Guid.NewGuid() };

        // Act
        var result = await _sut.UpsertAsync(photograph, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenDeletingAPhotograph()
    {
        // Arrange
        // Act
        var result = await _sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }
}
