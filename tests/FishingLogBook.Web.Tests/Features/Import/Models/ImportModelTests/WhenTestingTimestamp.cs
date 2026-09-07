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
    public void ItShouldKeepALocalWallClockUnresolvedUntilTheBrowserResolvesIt()
    {
        // Arrange
        var wallClock = new DateTime(2024, 6, 14, 9, 20, 0, DateTimeKind.Local);
        var proposed = ImportTimestampModel.FromLocalWallClock(
            wallClock,
            ImportTimestampSourceEnum.ExifOriginal);

        // Act
        var edited = proposed.Confirm(wallClock);

        // Assert
        edited.Instant.Should().BeNull();
        edited.LocalWallClock.Should().Be(DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified));
        edited.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void ItShouldRepresentTheInstantResolvedByTheBrowser()
    {
        // Arrange
        var resolved = new DateTimeOffset(2024, 6, 14, 9, 20, 0, TimeSpan.FromHours(4));

        // Act
        var confirmed = ImportTimestampModel.UserConfirmed(resolved);

        // Assert
        confirmed.State.Should().Be(ImportTimestampStateEnum.UserConfirmed);
        confirmed.Instant.Should().Be(resolved);
        confirmed.LocalWallClock.Should().BeNull();
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
            .Confirm(original);

        // Act
        var edited = confirmed.EditLocalWallClock(original.AddMinutes(10));

        // Assert
        edited.State.Should().Be(ImportTimestampStateEnum.LocalWallClock);
        edited.Instant.Should().BeNull();
        edited.LocalWallClock.Should().Be(original.AddMinutes(10));
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
