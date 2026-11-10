using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.ProfileRepositoryTests;

public class WhenTestingDatabaseFailure
{
    private readonly RecordingLogger<ProfileRepository> _logger = new();
    private readonly ProfileRepository _sut;

    public WhenTestingDatabaseFailure()
    {
        _sut = new ProfileRepository(FailingConnectionFactory.Create(), _logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenCheckingThatAUserExists()
    {
        // Arrange
        // Act
        var result = await _sut.UserExistsAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingAProfile()
    {
        // Arrange
        // Act
        var result = await _sut.GetByUserIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenLoadingSeveralProfiles()
    {
        // Arrange
        // Act
        var result = await _sut.GetByUserIdsAsync([Guid.NewGuid()], CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenFindingAnglers()
    {
        // Arrange
        var args = new FindAnglersArgs { RequestingUserId = Guid.NewGuid(), Query = "sam", MaxResults = 5 };

        // Act
        var result = await _sut.FindAnglersAsync(args, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenSavingAProfile()
    {
        // Arrange
        var profile = new Profile { UserId = Guid.NewGuid() };

        // Act
        var result = await _sut.UpsertAsync(profile, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenCompletingOnboarding()
    {
        // Arrange
        // Act
        var result = await _sut.CompleteOnboardingAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenUpdatingThePhotograph()
    {
        // Arrange
        var args = new RecordProfilePhotographArgs
        {
            UserId = Guid.NewGuid(),
            PhotographId = Guid.NewGuid(),
            ObjectKey = "profiles/key.jpg",
            ContentType = "image/jpeg"
        };

        // Act
        var result = await _sut.UpdatePhotographAsync(args, CancellationToken.None);

        // Assert
        result.ShouldHaveFailedAndLoggedTheException(_logger);
    }
}
