using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Common;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Tests.Common.Builders;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.RepositoryFailureTests.UserIdentityRepositoryTests;

public class WhenTestingDatabaseFailure
{
    private readonly IDbConnectionFactory _mockConnectionFactory = FailingConnectionFactory.Create();
    private readonly RecordingLogger<UserIdentityRepository> _logger = new();
    private readonly UserIdentityRepository _sut;

    public WhenTestingDatabaseFailure()
    {
        _sut = new UserIdentityRepository(_mockConnectionFactory, _logger);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenFindingAnIdentity()
    {
        // Arrange
        var args = new FindUserIdentityArgs { Provider = IdentityProviderConstants.Cognito, Subject = "subject" };

        // Act
        var result = await _sut.FindUserIdAsync(args, CancellationToken.None);

        // Assert
        AssertFailedAndLogged(result.IsFailed, result.Errors[0].Message);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenCreatingAnIdentity()
    {
        // Arrange
        var user = new UserBuilder().Build();
        var identity = new UserIdentityBuilder().ForUser(user).Build();

        // Act
        var result = await _sut.CreateAsync(user, identity, CancellationToken.None);

        // Assert
        AssertFailedAndLogged(result.IsFailed, result.Errors[0].Message);
    }

    [Fact]
    public async Task ItShouldFailAndLogWhenUpdatingTheEmail()
    {
        // Arrange
        var user = new UserBuilder().Build();

        // Act
        var result = await _sut.UpdateEmailAsync(user, CancellationToken.None);

        // Assert
        AssertFailedAndLogged(result.IsFailed, result.Errors[0].Message);
    }

    private void AssertFailedAndLogged(bool failed, string message)
    {
        failed.Should().BeTrue();
        message.Should().Be("Failed to resolve FishingLogBook user.");
        message.Should().NotContain(FailingConnectionFactory.FailureMessage);
        var record = _logger.Records.Should().ContainSingle().Subject;
        record.Level.Should().Be(LogLevel.Error);
        record.Exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be(FailingConnectionFactory.FailureMessage);
    }
}
