using System.Text;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Services;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.PhotoMetadataServiceTests;

public class WhenTestingJpegStructure : BasePhotoMetadataServiceTest
{
    [Fact]
    public void ItShouldRejectAJpegThatNeverReachesEndOfImage()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange([0xFF, 0xDB, 0x00, 0x05, 0x00, 0x01, 0x02]);

        // Act
        var sanitised = sut.Sanitise([.. bytes], PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised.Should().BeNull();
    }

    [Fact]
    public void ItShouldRejectAJpegWithACorruptSegmentLength()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange([0xFF, 0xE0, 0x7F, 0xFF]);
        bytes.AddRange([0xFF, 0xD9]);

        // Act
        var sanitised = sut.Sanitise([.. bytes], PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised.Should().BeNull();
    }

    [Fact]
    public void ItShouldRemoveMetadataThatFollowsTheScanAndDropTrailingBytes()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange(Scan());
        bytes.AddRange([0xFF, 0xE1]);
        var exif = new List<byte>(Encoding.ASCII.GetBytes("Exif\0\0"));
        exif.AddRange(Tiff(
            new ExifContent { Latitude = 53.2707, Longitude = -9.0568 },
            bigEndian: false));
        bytes.AddRange(BigEndian((ushort)(exif.Count + 2)));
        bytes.AddRange(exif);
        bytes.AddRange([0xFF, 0xD9]);
        bytes.AddRange(Encoding.ASCII.GetBytes("TRAILING-GPS-53.2707"));

        // Act
        var sanitised = sut.Sanitise([.. bytes], PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised.Should().NotBeNull();
        var text = Encoding.ASCII.GetString(sanitised!);
        text.Should().NotContain("Exif");
        text.Should().NotContain("TRAILING-GPS");
        sanitised!.Should().EndWith([(byte)0xFF, (byte)0xD9]);
        ScanPayload(sanitised).Should().NotBeEmpty();
    }

    [Fact]
    public void ItShouldKeepEveryScanOfAProgressiveJpeg()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange([0xFF, 0xE1, 0x00, 0x08, 0x45, 0x78, 0x69, 0x66, 0x00, 0x00]);
        bytes.AddRange(Scan());
        bytes.AddRange(Scan());
        bytes.AddRange(Scan());
        bytes.AddRange([0xFF, 0xD9]);

        // Act
        var sanitised = sut.Sanitise([.. bytes], PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised.Should().NotBeNull();
        Encoding.ASCII.GetString(sanitised!).Should().NotContain("Exif");
        CountScans(sanitised!).Should().Be(3);
        sanitised!.Should().EndWith([(byte)0xFF, (byte)0xD9]);
    }

    [Fact]
    public void ItShouldPreserveRestartMarkersAndStuffedBytesInsideTheScan()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var entropy = new byte[] { 0x9A, 0xFF, 0x00, 0x2B, 0xFF, 0xD0, 0x7C, 0xFF, 0x00, 0x11 };
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange([0xFF, 0xFE, 0x00, 0x08, 0x53, 0x45, 0x43, 0x52, 0x45, 0x54]);
        bytes.AddRange([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]);
        bytes.AddRange(entropy);
        bytes.AddRange([0xFF, 0xD9]);

        // Act
        var sanitised = sut.Sanitise([.. bytes], PhotographContentTypeConstants.Jpeg);

        // Assert
        sanitised.Should().NotBeNull();
        Encoding.ASCII.GetString(sanitised!).Should().NotContain("SECRET");
        var scan = ScanPayload(sanitised!);
        scan.Skip(10).Take(entropy.Length).Should().Equal(entropy);
    }

    private static byte[] Scan()
    {
        return
        [
            0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,
            0x9A, 0x2B, 0x7C, 0x11
        ];
    }

    private static int CountScans(byte[] jpeg)
    {
        var count = 0;
        for (var index = 0; index + 1 < jpeg.Length; index++)
        {
            if (jpeg[index] == 0xFF && jpeg[index + 1] == 0xDA)
            {
                count++;
            }
        }

        return count;
    }
}
