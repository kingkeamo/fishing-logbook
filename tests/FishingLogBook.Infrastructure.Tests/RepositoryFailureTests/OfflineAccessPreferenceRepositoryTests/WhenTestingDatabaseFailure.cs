using AwesomeAssertions;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.OfflineAccessPreferenceRepositoryTests;

public class WhenTestingDatabaseFailure
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IDbConnectionFactory _mockConnectionFactory = FailingConnectionFactory.Create();
    private readonly RecordingLogger<OfflineAccessPreferenceRepository> _logger = new();
    private readonly OfflineAccessPreferenceRepository _sut;

    public WhenTestingDatabaseFailure()
    {
        _sut = new OfflineAccessPreferenceRepository(_mockConnectionFactory, _logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenReadingThePreference()
    {
        // Arrange
        // Act
        var result = await _sut.GetAsync(UserId, CancellationToken.None);

        // Assert
        AssertFailedAndLogged(result.IsFailed, result.Errors[0].Message);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenWritingThePreference()
    {
        // Arrange
        // Act
        var result = await _sut.SetAsync(UserId, true, CancellationToken.None);

        // Assert
        AssertFailedAndLogged(result.IsFailed, result.Errors[0].Message);
    }

    private void AssertFailedAndLogged(bool failed, string message)
    {
        failed.Should().BeTrue();
        message.Should().NotContain(FailingConnectionFactory.FailureMessage);
        var record = _logger.Records.Should().ContainSingle().Subject;
        record.Level.Should().Be(LogLevel.Error);
        record.Exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be(FailingConnectionFactory.FailureMessage);
    }
}
