using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Common.Offline.OutboxBackoffPolicyTests;

public sealed class WhenTestingIsExhausted : BaseOutboxBackoffPolicyTest
{
    [Fact]
    public void ItShouldNotBeExhaustedForZeroOrNegativeAttempts()
    {
        // Arrange
        var sut = CreatePolicy(0.5);

        // Act
        var zero = sut.IsExhausted(0);
        var negative = sut.IsExhausted(-3);

        // Assert
        zero.Should().BeFalse();
        negative.Should().BeFalse();
    }

    [Fact]
    public void ItShouldNotBeExhaustedOneAttemptBelowTheMaximum()
    {
        // Arrange
        var sut = CreatePolicy(0.5);

        // Act
        var result = sut.IsExhausted(MaximumAttempts - 1);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldBeExhaustedAtTheMaximumAttempts()
    {
        // Arrange
        var sut = CreatePolicy(0.5);

        // Act
        var result = sut.IsExhausted(MaximumAttempts);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ItShouldBeExhaustedBeyondTheMaximumAttempts()
    {
        // Arrange
        var sut = CreatePolicy(0.5);

        // Act
        var beyond = sut.IsExhausted(MaximumAttempts + 1);
        var huge = sut.IsExhausted(int.MaxValue);

        // Assert
        beyond.Should().BeTrue();
        huge.Should().BeTrue();
    }
}
