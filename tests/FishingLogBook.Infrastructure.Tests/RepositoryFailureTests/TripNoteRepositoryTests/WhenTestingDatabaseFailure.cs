using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.TripNoteRepositoryTests;

public class WhenTestingDatabaseFailure
{
    private readonly RecordingLogger<TripNoteRepository> _logger = new();
    private readonly TripNoteRepository _sut;

    public WhenTestingDatabaseFailure()
    {
        _sut = new TripNoteRepository(FailingConnectionFactory.Create(), _logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingANote()
    {
        // Arrange
        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingTheNotesOfATrip()
    {
        // Arrange
        // Act
        var result = await _sut.GetByTripIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenSavingANote()
    {
        // Arrange
        var note = new TripNote { Id = Guid.NewGuid(), TripId = Guid.NewGuid(), Text = "Fish rising" };

        // Act
        var result = await _sut.UpsertAsync(note, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenDeletingANote()
    {
        // Arrange
        // Act
        var result = await _sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }
}
