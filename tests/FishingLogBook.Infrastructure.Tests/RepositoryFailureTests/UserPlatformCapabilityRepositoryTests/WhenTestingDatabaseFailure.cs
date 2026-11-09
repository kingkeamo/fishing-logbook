using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Users;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.UserPlatformCapabilityRepositoryTests;

public class WhenTestingDatabaseFailure
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly PlatformCapabilityEnum Capability = Enum.GetValues<PlatformCapabilityEnum>()[0];

    private readonly IDbConnectionFactory _mockConnectionFactory = FailingConnectionFactory.Create();
    private readonly RecordingLogger<UserPlatformCapabilityRepository> _logger = new();
    private readonly UserPlatformCapabilityRepository _sut;

    public WhenTestingDatabaseFailure()
    {
        _sut = new UserPlatformCapabilityRepository(_mockConnectionFactory, _logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenCheckingACapability()
    {
        // Arrange
        var args = new FindUserPlatformCapabilityArgs { UserId = UserId, Capability = Capability };

        // Act
        var result = await _sut.HasAsync(args, CancellationToken.None);

        // Assert
        AssertFailedAndLogged(result.IsFailed, result.Errors[0].Message);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenListingCapabilities()
    {
        // Arrange
        // Act
        var result = await _sut.GetForUserAsync(new FindUserPlatformCapabilitiesArgs { UserId = UserId }, CancellationToken.None);

        // Assert
        AssertFailedAndLogged(result.IsFailed, result.Errors[0].Message);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenGrantingACapability()
    {
        // Arrange
        var association = new UserPlatformCapability
        {
            UserId = UserId,
            Capability = Capability,
            CreatedOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z")
        };

        // Act
        var result = await _sut.GrantAsync(association, CancellationToken.None);

        // Assert
        AssertFailedAndLogged(result.IsFailed, result.Errors[0].Message);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenRevokingACapability()
    {
        // Arrange
        var args = new FindUserPlatformCapabilityArgs { UserId = UserId, Capability = Capability };

        // Act
        var result = await _sut.RevokeAsync(args, CancellationToken.None);

        // Assert
        AssertFailedAndLogged(result.IsFailed, result.Errors[0].Message);
    }

    private void AssertFailedAndLogged(bool failed, string message)
    {
        failed.Should().BeTrue();
        message.Should().Be("Failed to persist platform capability.");
        var record = _logger.Records.Should().ContainSingle().Subject;
        record.Level.Should().Be(LogLevel.Error);
        record.Exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be(FailingConnectionFactory.FailureMessage);
    }
}
