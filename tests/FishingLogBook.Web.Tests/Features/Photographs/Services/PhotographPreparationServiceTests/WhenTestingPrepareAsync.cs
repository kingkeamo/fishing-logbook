using System.Text;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Photographs.Services.PhotographPreparationServiceTests;

public class WhenTestingPrepareAsync : BasePhotographPreparationServiceTest
{
    [Fact]
    public async Task ItShouldRejectAnUnsupportedContentTypeWithoutReadingTheFile()
    {
        // Arrange
        var metadata = Substitute.For<IPhotographMetadataService>();
        var sut = CreateSut(metadata);

        // Act
        var result = await sut.PrepareAsync(
            File(JpegWithoutExif(), "image/heic"),
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.UnsupportedContentType);
        result.Photograph.Should().BeNull();
        metadata.DidNotReceive().Sanitise(Arg.Any<byte[]>(), Arg.Any<string>());
        await metadata.DidNotReceive().ReadAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReturnTheOriginalBytesWhenSanitisationCannotBeProven()
    {
        // Arrange
        var original = Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" } });
        var metadata = Substitute.For<IPhotographMetadataService>();
        metadata.ReadAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(PhotographMetadataModel.Empty);
        metadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>()).Returns((byte[]?)null);
        var sut = CreateSut(metadata);

        // Act
        var result = await sut.PrepareAsync(
            File(original),
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.CouldNotPrepare);
        result.Photograph.Should().BeNull();
        metadata.Received(1).Sanitise(
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(original)),
            PhotographContentTypeConstants.Jpeg);
    }

    [Fact]
    public async Task ItShouldLogSafelyAndRejectWhenSanitisationThrows()
    {
        // Arrange
        var metadata = Substitute.For<IPhotographMetadataService>();
        metadata.ReadAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(PhotographMetadataModel.Empty);
        metadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns<byte[]?>(_ => throw new InvalidOperationException("GPS 53.2707,-9.0568 at offset 42 in beach.jpg"));
        var logging = QuietLogging();
        var sut = CreateSut(metadata, logging);

        // Act
        var result = await sut.PrepareAsync(
            File(JpegWithoutExif()),
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.CouldNotPrepare);
        await logging.Received(1).LogErrorAsync(
            "removing photograph metadata",
            "Photograph metadata could not be removed (InvalidOperationException).",
            Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogSafelyAndRejectWhenTheFileCannotBeRead()
    {
        // Arrange
        var logging = QuietLogging();
        var sut = CreateSut(logging: logging);

        // Act
        var result = await sut.PrepareAsync(
            UnreadableFile(),
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.CouldNotPrepare);
        result.Photograph.Should().BeNull();
        await logging.Received(1).LogErrorAsync(
            "reading a selected photograph",
            "A photograph could not be read (IOException).",
            Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepPreparingWhenMetadataCannotBeReadSafely()
    {
        // Arrange
        var metadata = Substitute.For<IPhotographMetadataService>();
        metadata.ReadAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<PhotographMetadataModel>>(_ =>
                throw new InvalidOperationException("EXIF GPS 53.2707,-9.0568 at offset 42 in beach.jpg"));
        metadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<byte[]>(0));
        var logging = QuietLogging();
        var sut = CreateSut(metadata, logging);

        // Act
        var result = await sut.PrepareAsync(
            File(JpegWithoutExif()),
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
        result.Photograph!.Metadata.Should().Be(PhotographMetadataModel.Empty);
        await logging.Received(1).LogErrorAsync(
            "reading photograph metadata",
            "Photograph metadata could not be read (InvalidOperationException).",
            Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAMalformedSupportedContainer()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.PrepareAsync(
            File([0xFF, 0xD8, 0xFF, 0xDB, 0x00, 0x05, 0x00, 0x01, 0x02]),
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.CouldNotPrepare);
        result.Photograph.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotGiveACameraPhotographHistoricalSemantics()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.PrepareAsync(
            File(Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" }, Latitude = 53.2707, Longitude = -9.0568 })),
            PhotographSourceEnum.Camera,
            CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
        result.Photograph!.Source.Should().Be(PhotographSourceEnum.Camera);
        result.Photograph.FromCamera.Should().BeTrue();
        result.Photograph.Metadata.Should().Be(PhotographMetadataModel.Empty);
        result.Photograph.CapturedOnLocal.Should().BeNull();
        Encoding.ASCII.GetString(result.Photograph.Bytes).Should().NotContain("Exif");
    }

    [Fact]
    public async Task ItShouldFallBackToTheFileTimestampWhenNoExifDateExists()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.PrepareAsync(
            File(PngWithoutMetadata(), PhotographContentTypeConstants.Png, FileModifiedOn),
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
        result.Photograph!.Metadata.CapturedOn.Should().Be(FileModifiedOn);
        result.Photograph.Metadata.CapturedOnSource.Should()
            .Be(PhotographCapturedOnSourceEnum.FileLastModified);
        result.Photograph.Metadata.HasTrustworthyCapturedOn.Should().BeFalse();
        result.Photograph.CapturedOnLocal.Should().Be("2026-08-22T10:28");
    }

    [Fact]
    public async Task ItShouldNeverHandOnAFutureCaptureDateToConsumers()
    {
        // Arrange
        var sut = CreateSut();
        var original = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = "2099:01:01 09:00:00",
                [OffsetTimeOriginalTag] = "+00:00"
            },
            Latitude = 53.2707,
            Longitude = -9.0568
        });

        // Act
        var result = await sut.PrepareAsync(
            File(original, PhotographContentTypeConstants.Jpeg, DateTimeOffset.UtcNow.AddYears(5)),
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
        result.Photograph!.Metadata.CapturedOn.Should().BeNull();
        result.Photograph.Metadata.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.None);
        result.Photograph.CapturedOnLocal.Should().BeNull();
        result.Photograph.Metadata.HasCoordinates.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldPreferTheDigitizedDateWhenNoOriginalDateExists()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.PrepareAsync(
            File(Jpeg(new ExifContent { ExifText = { [DateTimeDigitizedTag] = "2025:06:14 07:32:10" } })),
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        // Assert
        result.Photograph!.Metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T07:32:10Z"));
        result.Photograph.Metadata.CapturedOnSource.Should()
            .Be(PhotographCapturedOnSourceEnum.ExifDigitized);
        result.Photograph.Metadata.HasTrustworthyCapturedOn.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldPrepareAGalleryJpegWithItsOriginalDateCoordinatesAndProvenance()
    {
        // Arrange
        var sut = CreateSut();
        var original = Jpeg(new ExifContent
        {
            ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" },
            Latitude = 53.2707,
            Longitude = -9.0568
        });

        // Act
        var result = await sut.PrepareAsync(
            File(original),
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
        var photograph = result.Photograph!;
        photograph.Source.Should().Be(PhotographSourceEnum.Gallery);
        photograph.ContentType.Should().Be(PhotographContentTypeConstants.Jpeg);
        photograph.Metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T07:32:10Z"));
        photograph.Metadata.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.ExifOriginal);
        photograph.Metadata.Latitude.Should().BeApproximately(53.2707, 0.0001);
        photograph.Metadata.Longitude.Should().BeApproximately(-9.0568, 0.0001);
        photograph.CapturedOnLocal.Should().Be("2025-06-14T07:32");
        photograph.Bytes.Should().NotEqual(original);
        Encoding.ASCII.GetString(photograph.Bytes).Should().NotContain("Exif");
    }

    [Fact]
    public async Task ItShouldRemoveMetadataFromEverySupportedContainerAndKeepOrientation()
    {
        // Arrange
        var sut = CreateSut();
        var containers = new (byte[] Bytes, string ContentType)[]
        {
            (Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" }, Latitude = 53.2707, Longitude = -9.0568, Orientation = 6 }), PhotographContentTypeConstants.Jpeg),
            (Png(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" }, Latitude = 53.2707, Longitude = -9.0568, Orientation = 6 }), PhotographContentTypeConstants.Png),
            (Webp(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" }, Latitude = 53.2707, Longitude = -9.0568, Orientation = 6 }, withExtendedHeader: true), PhotographContentTypeConstants.Webp)
        };

        foreach (var container in containers)
        {
            // Act
            var result = await sut.PrepareAsync(
                File(container.Bytes, container.ContentType),
                PhotographSourceEnum.Gallery,
                CancellationToken.None);

            // Assert
            result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
            result.Photograph!.Metadata.CapturedOn.Should().NotBeNull();
            ReadOrientationTag(result.Photograph.Bytes, container.ContentType).Should().Be(6);
            var reread = await new PhotographMetadataService(TestTimeService.WithOffset(TimeSpan.Zero)).ReadAsync(
                result.Photograph.Bytes,
                container.ContentType,
                null,
                ReferenceNow,
                CancellationToken.None);
            reread.CapturedOn.Should().BeNull();
            reread.HasCoordinates.Should().BeFalse();
        }
    }
}
