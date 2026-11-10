using AwesomeAssertions;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportPhotoPreparationServiceTests;

public class WhenTestingTimestampsAndFailures : BaseImportPhotoPreparationServiceTest
{
    private static PhotographHistoricalMetadataModel WallClock(DateTime wallClock)
    {
        return new PhotographHistoricalMetadataModel(
            null, wallClock, PhotographCapturedOnSourceEnum.ExifOriginal, true, false, null, null);
    }

    private static PhotographHistoricalMetadataModel Explicit(
        DateTimeOffset instant,
        PhotographCapturedOnSourceEnum source = PhotographCapturedOnSourceEnum.ExifOriginal)
    {
        return new PhotographHistoricalMetadataModel(instant, null, source, true, false, null, null);
    }

    [Fact]
    public async Task ItShouldFlagAPhotographAsFailedWhenItsMetadataCannotBeRemoved()
    {
        // Arrange
        var context = CreateContext();
        context.Metadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>()).Returns((byte[]?)null);

        // Act
        var result = await context.Sut.PrepareSelectionAsync([File(OriginalBytes)], CancellationToken.None);

        // Assert
        result.Single().PreparationStatus.Should().Be(ImportPhotoPreparationStatusEnum.PreparationFailed);
        result.Single().IsReady.Should().BeFalse();
        await context.Registry.DidNotReceive().RegisterAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldWarnWhenTwoDifferentPhotographsShareTheSameLocalWallClockAndSize()
    {
        // Arrange
        var context = CreateContext(WallClock(new DateTime(2025, 6, 14, 9, 30, 0)));
        context.Metadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<byte[]>(0));
        var files = new[] { File([1, 2, 3]), File([4, 5, 6]) };

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result[0].DuplicateStatus.Should().Be(ImportDuplicateStatusEnum.None);
        result[1].DuplicateStatus.Should().Be(ImportDuplicateStatusEnum.Warning);
        result[1].DuplicatePhotoIds.Should().Equal(result[0].Id);
    }

    [Fact]
    public async Task ItShouldNotWarnWhenTheLocalWallClocksDiffer()
    {
        // Arrange
        var variants = new[]
        {
            WallClock(new DateTime(2025, 6, 14, 9, 30, 0)),
            WallClock(new DateTime(2025, 6, 14, 9, 31, 0))
        };
        var context = CreateContext();
        var index = 0;
        context.Metadata.ReadHistorical(
                Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>())
            .Returns(_ => variants[index++]);
        context.Metadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<byte[]>(0));
        var files = new[] { File([1, 2, 3]), File([4, 5, 6]) };

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Should().OnlyContain(photo => photo.DuplicateStatus == ImportDuplicateStatusEnum.None);
    }

    [Fact]
    public async Task ItShouldNotWarnWhenTheExplicitInstantsDifferEvenWithTheSameSize()
    {
        // Arrange
        var first = DateTimeOffset.Parse("2025-06-14T09:30:00+01:00");
        var variants = new[] { Explicit(first), Explicit(first.AddMinutes(1)) };
        var context = CreateContext();
        var index = 0;
        context.Metadata.ReadHistorical(
                Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>())
            .Returns(_ => variants[index++]);
        context.Metadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<byte[]>(0));
        var files = new[] { File([1, 2, 3]), File([4, 5, 6]) };

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Should().OnlyContain(photo => photo.DuplicateStatus == ImportDuplicateStatusEnum.None);
    }

    [Theory]
    [InlineData(PhotographCapturedOnSourceEnum.ExifOriginal, ImportTimestampSourceEnum.ExifOriginal)]
    [InlineData(PhotographCapturedOnSourceEnum.ExifDigitized, ImportTimestampSourceEnum.ExifDigitized)]
    public async Task ItShouldRecordWhereAnExplicitCaptureTimeCameFrom(
        PhotographCapturedOnSourceEnum source,
        ImportTimestampSourceEnum expected)
    {
        // Arrange
        var context = CreateContext(Explicit(DateTimeOffset.Parse("2025-06-14T09:30:00+01:00"), source));

        // Act
        var result = await context.Sut.PrepareSelectionAsync([File(OriginalBytes)], CancellationToken.None);

        // Assert
        result.Single().Timestamp.State.Should().Be(ImportTimestampStateEnum.ExplicitInstant);
        result.Single().Timestamp.Source.Should().Be(expected);
    }

    [Theory]
    [InlineData(PhotographCapturedOnSourceEnum.ExifOriginal, ImportTimestampSourceEnum.ExifOriginal)]
    [InlineData(PhotographCapturedOnSourceEnum.ExifDigitized, ImportTimestampSourceEnum.ExifDigitized)]
    [InlineData(PhotographCapturedOnSourceEnum.FileLastModified, ImportTimestampSourceEnum.FileLastModified)]
    [InlineData(PhotographCapturedOnSourceEnum.None, ImportTimestampSourceEnum.None)]
    public async Task ItShouldRecordWhereAMalformedCaptureTimeCameFrom(
        PhotographCapturedOnSourceEnum source,
        ImportTimestampSourceEnum expected)
    {
        // Arrange
        var malformed = new PhotographHistoricalMetadataModel(null, null, source, true, true, null, null);
        var context = CreateContext(malformed);

        // Act
        var result = await context.Sut.PrepareSelectionAsync([File(OriginalBytes)], CancellationToken.None);

        // Assert
        result.Single().Timestamp.State.Should().Be(ImportTimestampStateEnum.Unusable);
        result.Single().Timestamp.Source.Should().Be(expected);
    }
}
