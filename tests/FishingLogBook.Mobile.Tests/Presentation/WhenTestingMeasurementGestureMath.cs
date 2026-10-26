using AwesomeAssertions;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Tests.Presentation;

public class WhenTestingMeasurementGestureMath
{
    [Theory]
    [InlineData(0, 1, 0.0)]
    [InlineData(1, 0, 0.25)]
    [InlineData(0, -1, 0.5)]
    [InlineData(-1, 0, 0.75)]
    public void ItShouldMapDialPositionsToClockwiseFractions(double dx, double dyUp, double expected)
    {
        // Act
        var result = MeasurementGestureMath.DialFraction(dx, dyUp, 0.5d);
        // Assert
        result.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void ItShouldPinTheDialToMaximumWhenDraggedClockwisePastTwelveFromTheEnd()
    {
        // Act
        var result = MeasurementGestureMath.DialFraction(0.2d, 1d, 0.95d);
        // Assert
        result.Should().Be(1d);
    }

    [Fact]
    public void ItShouldPinTheDialToMinimumWhenDraggedAnticlockwisePastTwelveFromTheStart()
    {
        // Act
        var result = MeasurementGestureMath.DialFraction(-0.2d, 1d, 0.05d);
        // Assert
        result.Should().Be(0d);
    }

    [Fact]
    public void ItShouldAllowTheDialToLeaveTheMaximumBackDownwards()
    {
        // Act
        var result = MeasurementGestureMath.DialFraction(-1d, 0d, 1d);
        // Assert
        result.Should().BeApproximately(0.75d, 1e-9);
    }

    [Fact]
    public void ItShouldFollowAFastJumpAcrossTheDialWithoutWrapping()
    {
        // Act
        var result = MeasurementGestureMath.DialFraction(0d, -1d, 0.1d);
        // Assert
        result.Should().BeApproximately(0.5d, 1e-9);
    }

    [Fact]
    public void ItShouldKeepTheCurrentFractionWhenTheTouchIsExactlyOnTheDialCentre()
    {
        // Act
        var result = MeasurementGestureMath.DialFraction(0d, 0d, 0.4d);
        // Assert
        result.Should().Be(0.4d);
    }

    [Theory]
    [InlineData(62, 0.0)]
    [InlineData(228, 1.0)]
    [InlineData(145, 0.5)]
    [InlineData(-40, 0.0)]
    [InlineData(500, 1.0)]
    public void ItShouldMapTapePositionsAndClampBeyondTheEnds(double x, double expected)
    {
        // Act
        var result = MeasurementGestureMath.TapeFraction(x, 0d, 1d);
        // Assert
        result.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void ItShouldAccountForScaleAndOffsetOnTheTape()
    {
        // Act
        var result = MeasurementGestureMath.TapeFraction(10d + (145d * 2d), 10d, 2d);
        // Assert
        result.Should().BeApproximately(0.5d, 1e-9);
    }

    [Fact]
    public void ItShouldTreatAnUnmeasuredTapeScaleAsOne()
    {
        // Act
        var result = MeasurementGestureMath.TapeFraction(145d, 0d, 0d);
        // Assert
        result.Should().BeApproximately(0.5d, 1e-9);
    }
}
