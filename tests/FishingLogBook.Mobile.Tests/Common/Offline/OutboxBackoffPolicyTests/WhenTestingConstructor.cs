using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Services;

namespace FishingLogBook.Mobile.Tests.Common.Offline.OutboxBackoffPolicyTests;

public sealed class WhenTestingConstructor : BaseOutboxBackoffPolicyTest
{
    [Fact]
    public void ItShouldRejectAZeroBaseDelay()
    {
        // Arrange
        // Act
        var act = () => new OutboxBackoffPolicy(TimeSpan.Zero, MaximumDelay, MaximumAttempts, () => 0.5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ItShouldRejectANegativeBaseDelay()
    {
        // Arrange
        // Act
        var act = () => new OutboxBackoffPolicy(TimeSpan.FromSeconds(-1), MaximumDelay, MaximumAttempts, () => 0.5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ItShouldRejectAMaximumDelayBelowTheBaseDelay()
    {
        // Arrange
        // Act
        var act = () => new OutboxBackoffPolicy(
            BaseDelay,
            BaseDelay - TimeSpan.FromTicks(1),
            MaximumAttempts,
            () => 0.5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ItShouldRejectZeroMaximumAttempts()
    {
        // Arrange
        // Act
        var act = () => new OutboxBackoffPolicy(BaseDelay, MaximumDelay, 0, () => 0.5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ItShouldRejectANullRandomSource()
    {
        // Arrange
        // Act
        var act = () => new OutboxBackoffPolicy(BaseDelay, MaximumDelay, MaximumAttempts, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ItShouldAcceptAMaximumDelayEqualToTheBaseDelayAndASingleAttempt()
    {
        // Arrange
        // Act
        var policy = new OutboxBackoffPolicy(BaseDelay, BaseDelay, 1, () => 0.5);

        // Assert
        policy.MaximumAttempts.Should().Be(1);
    }

    [Fact]
    public void ItShouldExposeTheDefaultMaximumAttemptsFromTheParameterlessConstructor()
    {
        // Arrange
        // Act
        var policy = new OutboxBackoffPolicy();

        // Assert
        policy.MaximumAttempts.Should().Be(OutboxBackoffPolicy.DefaultMaximumAttempts);
    }
}
