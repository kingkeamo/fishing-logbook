using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Common.Offline.OutboxBackoffPolicyTests;

public sealed class WhenTestingNextDelay : BaseOutboxBackoffPolicyTest
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(4, 80)]
    public void ItShouldDoubleTheDelayForEachAttemptWithoutJitter(int attemptCount, double expectedSeconds)
    {
        // Arrange
        var sut = CreatePolicy(0.5);

        // Act
        var result = sut.NextDelay(attemptCount);

        // Assert
        result.Should().BeCloseTo(TimeSpan.FromSeconds(expectedSeconds), Precision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void ItShouldUseTheBaseDelayForZeroOrNegativeAttempts(int attemptCount)
    {
        // Arrange
        var sut = CreatePolicy(0.5);

        // Act
        var result = sut.NextDelay(attemptCount);

        // Assert
        result.Should().BeCloseTo(BaseDelay, Precision);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(31)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void ItShouldCapTheDelayAtTheMaximumWithoutOverflowing(int attemptCount)
    {
        // Arrange
        var sut = CreatePolicy(0.5);

        // Act
        var result = sut.NextDelay(attemptCount);

        // Assert
        result.Should().BeCloseTo(MaximumDelay, Precision);
    }

    [Fact]
    public void ItShouldReduceTheDelayByTwentyPercentAtTheLowestJitter()
    {
        // Arrange
        var sut = CreatePolicy(0);

        // Act
        var result = sut.NextDelay(1);

        // Assert
        result.Should().BeCloseTo(TimeSpan.FromSeconds(8), Precision);
    }

    [Fact]
    public void ItShouldIncreaseTheDelayByTwentyPercentAtTheHighestJitter()
    {
        // Arrange
        var sut = CreatePolicy(1);

        // Act
        var result = sut.NextDelay(1);

        // Assert
        result.Should().BeCloseTo(TimeSpan.FromSeconds(12), Precision);
    }

    [Fact]
    public void ItShouldNotExceedTheMaximumDelayEvenWithHighestJitter()
    {
        // Arrange
        var sut = CreatePolicy(1);

        // Act
        var capped = sut.NextDelay(5);
        var nearCap = sut.NextDelay(4);

        // Assert
        capped.Should().BeCloseTo(MaximumDelay, Precision);
        nearCap.Should().BeCloseTo(TimeSpan.FromSeconds(96), Precision);
    }

    [Fact]
    public void ItShouldApplyLowestJitterToTheCappedDelay()
    {
        // Arrange
        var sut = CreatePolicy(0);

        // Act
        var result = sut.NextDelay(10);

        // Assert
        result.Should().BeCloseTo(TimeSpan.FromSeconds(80), Precision);
    }

    [Theory]
    [InlineData(-5, 8)]
    [InlineData(7, 12)]
    public void ItShouldClampRandomValuesOutsideTheUnitRange(double random, double expectedSeconds)
    {
        // Arrange
        var sut = CreatePolicy(random);

        // Act
        var result = sut.NextDelay(1);

        // Assert
        result.Should().BeCloseTo(TimeSpan.FromSeconds(expectedSeconds), Precision);
    }

    [Theory]
    [InlineData(0.25, 9)]
    [InlineData(0.75, 11)]
    public void ItShouldScaleJitterLinearlyBetweenTheBounds(double random, double expectedSeconds)
    {
        // Arrange
        var sut = CreatePolicy(random);

        // Act
        var result = sut.NextDelay(1);

        // Assert
        result.Should().BeCloseTo(TimeSpan.FromSeconds(expectedSeconds), Precision);
    }
}
