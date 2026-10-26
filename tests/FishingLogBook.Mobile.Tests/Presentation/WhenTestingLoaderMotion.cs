using AwesomeAssertions;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Tests.Presentation;

public sealed class WhenTestingLoaderMotion
{
    [Theory]
    [InlineData(1.25, 0.25)]
    [InlineData(-0.25, 0.75)]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    public void ThenPhaseWrapsIntoOneCycle(double phase, double expected)
    {
        // Act
        var result = LoaderMotion.NormalisePhase(phase);

        // Assert
        result.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData(LoaderKindEnum.LeapingFish, 1800u)]
    [InlineData(LoaderKindEnum.Boat, 1600u)]
    [InlineData(LoaderKindEnum.Bobber, 2600u)]
    public void ThenEachLoaderHasItsOwnCycleLength(LoaderKindEnum kind, uint expected)
    {
        // Act
        var result = LoaderMotion.CycleMilliseconds(kind);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(LoaderKindEnum.LeapingFish, 0.5)]
    [InlineData(LoaderKindEnum.Boat, 0)]
    [InlineData(LoaderKindEnum.Bobber, 0)]
    public void ThenStillPhaseShowsTheRecognisablePose(LoaderKindEnum kind, double expected)
    {
        // Act
        var result = LoaderMotion.StillPhase(kind);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ThenLeapingFishIsHighestAtMidCycleAndHiddenAtTheEnds()
    {
        // Act
        var start = LoaderMotion.LeapingFish(0);
        var peak = LoaderMotion.LeapingFish(0.5);
        var end = LoaderMotion.LeapingFish(1);

        // Assert
        peak.Y.Should().Be(20);
        peak.Opacity.Should().Be(1);
        start.Opacity.Should().Be(0);
        end.Opacity.Should().Be(0);
    }

    [Fact]
    public void ThenLeapingFishLoopsSeamlesslyInPosition()
    {
        // Act
        var justBefore = LoaderMotion.LeapingFish(0.999);
        var justAfter = LoaderMotion.LeapingFish(1.001);

        // Assert
        justBefore.Opacity.Should().BeLessThan(0.05);
        justAfter.Opacity.Should().BeLessThan(0.05);
    }

    [Fact]
    public void ThenSamplingNeverLeavesTheKeyframeBounds()
    {
        for (var step = 0; step <= 200; step++)
        {
            // Act
            var phase = step / 200d;
            var rig = LoaderMotion.BobberRig(phase);
            var fish = LoaderMotion.BobberFish(phase);
            var leave = LoaderMotion.LeapSplashLeave(phase);

            // Assert
            rig.Y.Should().BeInRange(0, 22);
            fish.Opacity.Should().BeInRange(0, 1);
            leave.Scale.Should().BeInRange(0.4, 1.5);
        }
    }

    [Fact]
    public void ThenBobberIsPulledUnderAndReturns()
    {
        // Act
        var rest = LoaderMotion.BobberRig(0.1);
        var under = LoaderMotion.BobberRig(0.6);
        var back = LoaderMotion.BobberRig(1);

        // Assert
        rest.Y.Should().Be(0);
        under.Y.Should().Be(22);
        back.Y.Should().Be(0);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0.32, true)]
    [InlineData(0.33, false)]
    [InlineData(0.9, false)]
    public void ThenBaitIsVisibleOnlyBeforeTheTake(double phase, bool expected)
    {
        // Act
        var result = LoaderMotion.IsBaitVisible(phase);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ThenBoatPitchStaysBetweenSixAndTenDegrees()
    {
        for (var step = 0; step <= 100; step++)
        {
            // Act
            var pitch = LoaderMotion.BoatPitch(step / 100d);

            // Assert
            pitch.Should().BeInRange(6, 10);
        }
    }

    [Fact]
    public void ThenPropellerScaleFlickersWithinRange()
    {
        for (var step = 0; step <= 200; step++)
        {
            // Act
            var scale = LoaderMotion.PropScale(step / 200d);

            // Assert
            scale.Should().BeInRange(0.2 - 1e-9, 1 + 1e-9);
        }
    }

    [Fact]
    public void ThenWakeDashesMoveByWholePeriodsPerCycle()
    {
        // Act
        var start = LoaderMotion.WakeDashOffset(0);
        var almostEnd = LoaderMotion.WakeDashOffset(0.9999);

        // Assert
        start.Should().Be(0);
        almostEnd.Should().BeApproximately(-40, 0.01);
    }

    [Fact]
    public void ThenWaveDelayShiftsTheSwing()
    {
        // Act
        var first = LoaderMotion.WaveOffset(0.5);
        var delayed = LoaderMotion.WaveOffset(0.5, 0.5);

        // Assert
        first.Should().BeApproximately(-3, 1e-9);
        delayed.Should().BeApproximately(0, 1e-9);
    }
}
