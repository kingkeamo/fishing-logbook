using AwesomeAssertions;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingTimestamp : BaseImportModelTest
{
    [Fact]
    public void ItShouldRepresentAnExplicitExifInstantAsResolved()
    {
        // Arrange
        var timestamp = ImportTimestampModel.FromExplicitInstant(
            CapturedOn,
            ImportTimestampSourceEnum.ExifOriginal);

        // Act
        var resolved = timestamp.IsResolved;

        // Assert
        resolved.Should().BeTrue();
        timestamp.Instant.Should().Be(CapturedOn);
        timestamp.LocalWallClock.Should().BeNull();
        timestamp.HasTimezoneAmbiguity.Should().BeFalse();
    }

    [Fact]
    public void ItShouldKeepALocalExifTimeAmbiguousAndUnresolved()
    {
        // Arrange
        var local = new DateTime(2025, 6, 14, 9, 30, 0, DateTimeKind.Local);

        // Act
        var timestamp = ImportTimestampModel.FromLocalWallClock(
            local,
            ImportTimestampSourceEnum.ExifDigitized);

        // Assert
        timestamp.State.Should().Be(ImportTimestampStateEnum.LocalWallClock);
        timestamp.LocalWallClock.Should().Be(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
        timestamp.HasTimezoneAmbiguity.Should().BeTrue();
        timestamp.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void ItShouldKeepFileLastModifiedAsAWeakUnresolvedFallback()
    {
        // Arrange
        var timestamp = ImportTimestampModel.FromWeakFallback(CapturedOn);

        // Act
        var resolved = timestamp.IsResolved;

        // Assert
        resolved.Should().BeFalse();
        timestamp.State.Should().Be(ImportTimestampStateEnum.WeakFallback);
        timestamp.Source.Should().Be(ImportTimestampSourceEnum.FileLastModified);
    }

    [Theory]
    [InlineData(ImportTimestampStateEnum.Missing)]
    [InlineData(ImportTimestampStateEnum.Unusable)]
    public void ItShouldRepresentMissingAndUnusableTimestamps(ImportTimestampStateEnum state)
    {
        // Arrange
        var timestamp = state == ImportTimestampStateEnum.Missing
            ? ImportTimestampModel.Missing()
            : ImportTimestampModel.Unusable(ImportTimestampSourceEnum.ExifOriginal);

        // Act
        var resolved = timestamp.IsResolved;

        // Assert
        timestamp.State.Should().Be(state);
        resolved.Should().BeFalse();
    }

    [Fact]
    public void ItShouldMakeAUserConfirmedTimestampResolved()
    {
        // Arrange
        var timestamp = ImportTimestampModel.UserConfirmed(CapturedOn);

        // Act
        var resolved = timestamp.IsResolved;

        // Assert
        resolved.Should().BeTrue();
        timestamp.Source.Should().Be(ImportTimestampSourceEnum.User);
    }

    [Fact]
    public void ItShouldRequireAnExplicitOffsetToConfirmALocalWallClock()
    {
        // Arrange
        var wallClock = new DateTime(2024, 6, 14, 9, 20, 0, DateTimeKind.Local);
        var proposed = ImportTimestampModel.FromLocalWallClock(
            wallClock,
            ImportTimestampSourceEnum.ExifOriginal);

        // Act
        Action confirm = () => proposed.Confirm(wallClock);

        // Assert
        confirm.Should().Throw<InvalidOperationException>()
            .WithMessage("*explicit UTC offset*");
        proposed.RequiresUtcOffset.Should().BeTrue();
        proposed.IsResolved.Should().BeFalse();
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(-5, 0)]
    [InlineData(5, 30)]
    public void ItShouldCreateAnExactUserConfirmedInstantFromALocalWallClockAndOffset(
        int offsetHours,
        int offsetMinutes)
    {
        // Arrange
        var wallClock = new DateTime(2024, 6, 14, 9, 20, 0, DateTimeKind.Local);
        var proposed = ImportTimestampModel.FromLocalWallClock(
            wallClock,
            ImportTimestampSourceEnum.ExifOriginal);
        var sign = offsetHours < 0 ? -1 : 1;
        var offset = new TimeSpan(offsetHours, sign * offsetMinutes, 0);

        // Act
        var confirmed = proposed.ConfirmLocalWallClock(wallClock, offset);

        // Assert
        confirmed.State.Should().Be(ImportTimestampStateEnum.UserConfirmed);
        confirmed.Instant.Should().Be(new DateTimeOffset(2024, 6, 14, 9, 20, 0, offset));
        confirmed.LocalWallClock.Should().Be(new DateTime(2024, 6, 14, 9, 20, 0, DateTimeKind.Unspecified));
        confirmed.RequiresUtcOffset.Should().BeFalse();
        confirmed.IsResolved.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRequireOffsetReconfirmationAfterEditingAConfirmedLocalWallClock()
    {
        // Arrange
        var original = new DateTime(2024, 6, 14, 9, 20, 0);
        var confirmed = ImportTimestampModel.FromLocalWallClock(
                original,
                ImportTimestampSourceEnum.ExifOriginal)
            .ConfirmLocalWallClock(original, TimeSpan.FromHours(1));

        // Act
        var edited = confirmed.EditLocalWallClock(original.AddMinutes(10));

        // Assert
        edited.State.Should().Be(ImportTimestampStateEnum.LocalWallClock);
        edited.Instant.Should().BeNull();
        edited.LocalWallClock.Should().Be(original.AddMinutes(10));
        edited.RequiresUtcOffset.Should().BeTrue();
        edited.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void ItShouldPreserveTheHistoricalOffsetWhenCorrectingAnExplicitInstant()
    {
        // Arrange
        var proposed = ImportTimestampModel.FromExplicitInstant(
            CapturedOn,
            ImportTimestampSourceEnum.ExifOriginal);
        var correction = new DateTime(2025, 6, 14, 10, 15, 0);

        // Act
        var confirmed = proposed.Confirm(correction);

        // Assert
        confirmed.Instant.Should().Be(new DateTimeOffset(correction, CapturedOn.Offset));
    }
}
