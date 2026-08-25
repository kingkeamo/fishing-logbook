using System.Text;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Services;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.PhotoMetadataServiceTests;

public class WhenTestingSanitise : BasePhotoMetadataServiceTest
{
    private const string CaptureWallClock = "2025:06:14 07:32:10";

    [Fact]
    public void ItShouldReturnTheBytesUnchangedWhenTheContentTypeIsNotSupported()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = CaptureWallClock } });

        // Act
        var sanitised = sut.Sanitise(bytes, "image/heic");

        // Assert
        sanitised.Should().BeNull();
    }

    [Fact]
    public void ItShouldReturnTheBytesUnchangedWhenTheContainerIsNotRecognised()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldRemoveTheJpegExifDateAndCoordinates()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent
        {
            ExifText = { [DateTimeOriginalTag] = CaptureWallClock },
            Latitude = 53.2707,
            Longitude = -9.0568
        });
        var original = await sut.ReadAsync(bytes, PhotographContentTypeConstants.Jpeg, null, CancellationToken.None);

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised!.Should().NotBeNull();
        original.CapturedOn.Should().NotBeNull();
        original.HasCoordinates.Should().BeTrue();
        var reread = await sut.ReadAsync(
            sanitised,
            PhotographContentTypeConstants.Jpeg,
            null,
            CancellationToken.None);
        reread.Should().Be(PhotoMetadataModel.Empty);
        Encoding.ASCII.GetString(sanitised!).Should().NotContain("Exif");
        sanitised!.Should().StartWith([(byte)0xFF, (byte)0xD8]);
        sanitised!.Length.Should().BeLessThan(bytes.Length);
    }

    [Fact]
    public async Task ItShouldRemoveTheJpegCommentAndPhotoshopSegments()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = JpegWithExtraSegments(
            new ExifContent { Latitude = 53.2707, Longitude = -9.0568 },
            (0xFE, "GPS 53.2707,-9.0568"u8.ToArray()),
            (0xED, "Photoshop 3.0\0IPTC location"u8.ToArray()),
            (0xE1, BuildXmpPayload()));

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised!.Should().NotBeNull();
        var text = Encoding.ASCII.GetString(sanitised!);
        text.Should().NotContain("GPS 53.2707");
        text.Should().NotContain("Photoshop");
        text.Should().NotContain("adobe:ns:meta");
        var reread = await sut.ReadAsync(
            sanitised,
            PhotographContentTypeConstants.Jpeg,
            null,
            CancellationToken.None);
        reread.HasCoordinates.Should().BeFalse();
    }

    [Fact]
    public void ItShouldKeepTheJpegImageDataAndColourSegments()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var iccPayload = Encoding.ASCII.GetBytes("ICC_PROFILE\0colour");
        var bytes = JpegWithExtraSegments(
            new ExifContent { ExifText = { [DateTimeOriginalTag] = CaptureWallClock } },
            (0xE2, iccPayload));

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised!.Should().NotBeNull();
        Encoding.ASCII.GetString(sanitised!).Should().Contain("ICC_PROFILE");
        Encoding.ASCII.GetString(sanitised!).Should().Contain("JFIF");
        sanitised!.Should().EndWith([(byte)0xFF, (byte)0xD9]);
        var scan = ScanPayload(sanitised!);
        scan.Should().NotBeEmpty();
        scan.Should().Equal(ScanPayload(bytes));
    }

    [Fact]
    public async Task ItShouldPreserveTheJpegOrientationWithoutTheRestOfTheExifBlock()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent
        {
            Orientation = 6,
            ExifText = { [DateTimeOriginalTag] = CaptureWallClock },
            Latitude = 53.2707,
            Longitude = -9.0568
        });

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised!.Should().NotBeNull();
        ReadOrientationTag(sanitised!, PhotographContentTypeConstants.Jpeg).Should().Be(6);
        var reread = await sut.ReadAsync(
            sanitised,
            PhotographContentTypeConstants.Jpeg,
            null,
            CancellationToken.None);
        reread.CapturedOn.Should().BeNull();
        reread.HasCoordinates.Should().BeFalse();
    }

    [Fact]
    public void ItShouldNotAddAnExifBlockWhenTheJpegOrientationIsTheDefault()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent
        {
            Orientation = 1,
            ExifText = { [DateTimeOriginalTag] = CaptureWallClock }
        });

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised!.Should().NotBeNull();
        Encoding.ASCII.GetString(sanitised!).Should().NotContain("Exif");
    }

    [Fact]
    public async Task ItShouldRemoveThePngExifAndTextChunks()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Png(new ExifContent
        {
            ExifText = { [DateTimeOriginalTag] = CaptureWallClock },
            Latitude = 53.2707,
            Longitude = -9.0568
        });

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Png);

        // Assert
        sanitised!.Should().NotBeNull();
        var reread = await sut.ReadAsync(
            sanitised,
            PhotographContentTypeConstants.Png,
            null,
            CancellationToken.None);
        reread.Should().Be(PhotoMetadataModel.Empty);
        Encoding.ASCII.GetString(sanitised!).Should().NotContain("eXIf");
        Encoding.ASCII.GetString(sanitised!).Should().NotContain("tEXt");
        PngChunkTypes(sanitised!).Should().Contain("IHDR").And.Contain("IDAT").And.Contain("IEND");
        PngChunksAreWellFormed(sanitised!).Should().BeTrue();
    }

    [Fact]
    public void ItShouldPreserveThePngOrientationChunkWithACorrectChecksum()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Png(new ExifContent
        {
            Orientation = 8,
            ExifText = { [DateTimeOriginalTag] = CaptureWallClock }
        });

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Png);

        // Assert
        sanitised!.Should().NotBeNull();
        ReadOrientationTag(sanitised!, PhotographContentTypeConstants.Png).Should().Be(8);
        PngChunksAreWellFormed(sanitised!).Should().BeTrue();
        PngChunkTypes(sanitised!).Should().EndWith(["eXIf", "IEND"]);
    }

    [Fact]
    public async Task ItShouldRemoveTheWebpExifChunkAndKeepTheRiffStructureValid()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Webp(new ExifContent
        {
            ExifText = { [DateTimeOriginalTag] = CaptureWallClock },
            Latitude = 53.2707,
            Longitude = -9.0568
        });

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Webp);

        // Assert
        sanitised!.Should().NotBeNull();
        var reread = await sut.ReadAsync(
            sanitised,
            PhotographContentTypeConstants.Webp,
            null,
            CancellationToken.None);
        reread.Should().Be(PhotoMetadataModel.Empty);
        Encoding.ASCII.GetString(sanitised!).Should().NotContain("EXIF");
        WebpRiffSizeMatches(sanitised!).Should().BeTrue();
        WebpChunkTypes(sanitised!).Should().Contain("VP8 ");
    }

    [Fact]
    public void ItShouldClearTheWebpExtendedFormatMetadataFlags()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Webp(
            new ExifContent { ExifText = { [DateTimeOriginalTag] = CaptureWallClock } },
            withExtendedHeader: true);

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Webp);

        // Assert
        sanitised!.Should().NotBeNull();
        WebpExtendedFlags(bytes).Should().Be(0x0C);
        WebpExtendedFlags(sanitised!).Should().Be(0x00);
        WebpRiffSizeMatches(sanitised!).Should().BeTrue();
    }

    [Fact]
    public void ItShouldPreserveTheWebpOrientationAndKeepItsExifFlag()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Webp(
            new ExifContent { Orientation = 3, ExifText = { [DateTimeOriginalTag] = CaptureWallClock } },
            withExtendedHeader: true);

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Webp);

        // Assert
        sanitised!.Should().NotBeNull();
        ReadOrientationTag(sanitised!, PhotographContentTypeConstants.Webp).Should().Be(3);
        (WebpExtendedFlags(sanitised!) & 0x08).Should().Be(0x08);
        (WebpExtendedFlags(sanitised!) & 0x04).Should().Be(0x00);
        WebpRiffSizeMatches(sanitised!).Should().BeTrue();
    }
}
