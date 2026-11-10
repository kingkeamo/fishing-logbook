using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Constants.CatchDetailConstantsTests;

public class WhenTestingValidation
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T12:00:00Z");

    [Fact]
    public void ItShouldRejectADefaultCaughtOnTime()
    {
        // Arrange
        // Act
        var result = CatchDetailConstants.IsCaughtOnValid(default, Now);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ItShouldAllowTheFutureSkewExactlyAndRejectTheSmallestIncrementBeyondIt()
    {
        // Arrange
        var limit = Now + CatchDetailConstants.MaxCaughtOnFutureSkew;

        // Act
        var atLimit = CatchDetailConstants.IsCaughtOnValid(limit, Now);
        var beyondLimit = CatchDetailConstants.IsCaughtOnValid(limit.AddTicks(1), Now);

        // Assert
        atLimit.Should().BeTrue();
        beyondLimit.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("0.01", true)]
    [InlineData("1000", true)]
    [InlineData("1000.01", false)]
    public void ItShouldValidateWeightAgainstTheCanonicalMaximum(string? weight, bool expected)
    {
        // Arrange
        decimal? value = weight is null ? null : decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var result = CatchDetailConstants.IsWeightValid(value);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("0.01", true)]
    [InlineData("1000", true)]
    [InlineData("1000.01", false)]
    public void ItShouldValidateLengthAgainstTheCanonicalMaximum(string? length, bool expected)
    {
        // Arrange
        decimal? value = length is null ? null : decimal.Parse(length, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var result = CatchDetailConstants.IsLengthValid(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ItShouldAcceptMissingOptionalTextAndTextAtTheLimitOnly()
    {
        // Arrange
        var atLimit = new string('a', 10);
        var overLimit = new string('a', 11);

        // Act
        var missing = CatchDetailConstants.IsOptionalTextValid(null, 10);
        var limit = CatchDetailConstants.IsOptionalTextValid(atLimit, 10);
        var beyond = CatchDetailConstants.IsOptionalTextValid(overLimit, 10);

        // Assert
        missing.Should().BeTrue();
        limit.Should().BeTrue();
        beyond.Should().BeFalse();
    }
}
