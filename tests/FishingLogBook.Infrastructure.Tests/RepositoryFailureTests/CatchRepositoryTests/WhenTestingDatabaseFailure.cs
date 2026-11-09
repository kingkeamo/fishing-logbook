using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.CatchRepositoryTests;

public class WhenTestingDatabaseFailure
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly RecordingLogger<CatchRepository> _logger = new();
    private readonly CatchRepository _sut;

    public WhenTestingDatabaseFailure()
    {
        _sut = new CatchRepository(FailingConnectionFactory.Create(), _logger, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingACatch()
    {
        // Arrange
        // Act
        var result = await _sut.GetByIdAsync(CatchId, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingACatchDetail()
    {
        // Arrange
        // Act
        var result = await _sut.GetDetailForUserAsync(CatchId, UserId, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingUserActivity()
    {
        // Arrange
        // Act
        var result = await _sut.GetActivityForUserAsync(UserId, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingAPhotograph()
    {
        // Arrange
        var args = new GetCatchPhotographArgs { CaughtByUserId = UserId, CatchId = CatchId, PhotographId = Guid.NewGuid() };

        // Act
        var result = await _sut.GetPhotographAsync(args, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenDeletingAPhotograph()
    {
        // Arrange
        var args = new GetCatchPhotographArgs { CaughtByUserId = UserId, CatchId = CatchId, PhotographId = Guid.NewGuid() };

        // Act
        var result = await _sut.DeletePhotographAsync(args, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenSavingACatch()
    {
        // Arrange
        var catchRecord = new Catch { Id = CatchId, CaughtByUserId = UserId, RecordedByUserId = UserId };

        // Act
        var result = await _sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenAssociatingATrip()
    {
        // Arrange
        var args = new PersistCatchTripArgs { CatchId = CatchId, CaughtByUserId = UserId, TripId = Guid.NewGuid() };

        // Act
        var result = await _sut.AssociateTripAsync(args, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenUpdatingLocationVisibility()
    {
        // Arrange
        var args = new PersistCatchLocationVisibilityArgs
        {
            CatchId = CatchId,
            CaughtByUserId = UserId,
            Visibility = LocationDefaults.Public
        };

        // Act
        var result = await _sut.UpdateLocationVisibilityAsync(args, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenCorrectingTheAngler()
    {
        // Arrange
        var args = new PersistCatchAnglerArgs { CatchId = CatchId, CaughtByUserId = UserId };

        // Act
        var result = await _sut.CorrectAnglerAsync(args, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }
}
