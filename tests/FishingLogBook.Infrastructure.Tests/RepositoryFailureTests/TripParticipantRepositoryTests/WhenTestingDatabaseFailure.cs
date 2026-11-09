using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.TripParticipantRepositoryTests;

public class WhenTestingDatabaseFailure
{
    private readonly RecordingLogger<TripParticipantRepository> _logger = new();
    private readonly TripParticipantRepository _sut;

    public WhenTestingDatabaseFailure()
    {
        _sut = new TripParticipantRepository(FailingConnectionFactory.Create(), _logger, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenFindingAParticipant()
    {
        // Arrange
        var args = new FindTripParticipantArgs { TripId = Guid.NewGuid(), UserId = Guid.NewGuid() };

        // Act
        var result = await _sut.FindAsync(args, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenListingTheParticipantsOfATrip()
    {
        // Arrange
        // Act
        var result = await _sut.GetByTripIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenListingPendingInvitations()
    {
        // Arrange
        // Act
        var result = await _sut.GetPendingInvitationsByUserIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenSavingAParticipant()
    {
        // Arrange
        var participant = new TripParticipant { Id = Guid.NewGuid(), TripId = Guid.NewGuid(), UserId = Guid.NewGuid() };

        // Act
        var result = await _sut.UpsertAsync(participant, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }
}
