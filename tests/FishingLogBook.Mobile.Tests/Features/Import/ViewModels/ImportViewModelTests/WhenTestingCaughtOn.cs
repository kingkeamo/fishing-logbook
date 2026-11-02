using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingCaughtOn : BaseImportViewModelTest
{
    [Fact]
    public async Task ItShouldDisplayTheExifWallClockWithoutConvertingToTheDeviceTimezone()
    {
        // Arrange
        var photoOffset = OffsetDifferentFromDevice();
        var capturedOn = new DateTimeOffset(2026, 11, 1, 15, 5, 0, photoOffset);

        // Act
        await ReviewPreparedPhotoAsync(ReadyPhoto(
            ImportTimestampModel.FromExplicitInstant(capturedOn, ImportTimestampSourceEnum.ExifOriginal)));

        // Assert
        var row = Sut.Catches.Single();
        row.CaughtOnDate.Should().Be(new DateTime(2026, 11, 1));
        row.CaughtOnTime.Should().Be(new TimeSpan(15, 5, 0));
        row.Proposal.CaughtOn.Instant.Should().Be(capturedOn);
    }

    [Fact]
    public async Task ItShouldKeepTheOriginalExifOffsetWhenTheDisplayedWallClockIsEdited()
    {
        // Arrange
        var photoOffset = OffsetDifferentFromDevice();
        var capturedOn = new DateTimeOffset(2026, 11, 1, 15, 5, 0, photoOffset);
        await ReviewPreparedPhotoAsync(ReadyPhoto(
            ImportTimestampModel.FromExplicitInstant(capturedOn, ImportTimestampSourceEnum.ExifOriginal)));
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        await Sut.ConfirmCaughtOnAsync(
            catchId,
            new DateTime(2026, 11, 1, 16, 10, 0, DateTimeKind.Unspecified),
            CancellationToken.None);

        // Assert
        Sut.Catches.Single().Proposal.CaughtOn.Instant.Should().Be(
            new DateTimeOffset(2026, 11, 1, 16, 10, 0, photoOffset));
        Sut.Catches.Single().CaughtOnTime.Should().Be(new TimeSpan(16, 10, 0));
    }

    [Fact]
    public async Task ItShouldResolveAMissingTimestampThroughTheDeviceLocalMapper()
    {
        // Arrange
        var mapped = new DateTimeOffset(2026, 4, 2, 9, 30, 0, TimeSpan.FromHours(4));
        MockLocalTime.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(mapped);

        // Act
        await ReviewPreparedPhotoAsync(ReadyPhoto(ImportTimestampModel.Missing()));

        // Assert
        Sut.Catches.Single().Proposal.CaughtOn.State.Should().Be(ImportTimestampStateEnum.UserConfirmed);
        Sut.Catches.Single().Proposal.CaughtOn.Instant.Should().Be(mapped);
        await MockLocalTime.Received().FromDateTimeLocalValueAsync(
            Arg.Is<string>(value => value.Length > 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotGuessATimezoneForAnAmbiguousExifWallClockUntilTheMapperResolvesIt()
    {
        // Arrange
        var wallClock = new DateTime(2026, 6, 14, 9, 30, 0, DateTimeKind.Unspecified);
        var mapped = new DateTimeOffset(wallClock, TimeSpan.FromHours(1));
        MockLocalTime.FromDateTimeLocalValueAsync("2026-06-14T09:30:00", Arg.Any<CancellationToken>())
            .Returns(mapped);

        // Act
        await ReviewPreparedPhotoAsync(ReadyPhoto(
            ImportTimestampModel.FromLocalWallClock(wallClock, ImportTimestampSourceEnum.ExifOriginal)));

        // Assert
        Sut.Catches.Single().Proposal.CaughtOn.Instant.Should().Be(mapped);
        Sut.Catches.Single().CaughtOnTime.Should().Be(new TimeSpan(9, 30, 0));
        await MockLocalTime.Received(1).FromDateTimeLocalValueAsync(
            "2026-06-14T09:30:00",
            Arg.Any<CancellationToken>());
    }

    private static TimeSpan OffsetDifferentFromDevice()
    {
        var device = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 1, 15, 5, 0));
        return device == TimeSpan.FromHours(5.5) ? TimeSpan.FromHours(-8) : TimeSpan.FromHours(5.5);
    }
}
