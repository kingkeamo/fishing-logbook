using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.PhotoMetadataServiceTests;

public class WhenTestingReadAsync : BasePhotoMetadataServiceTest
{
    [Fact]
    public async Task ItShouldReturnNothingWhenTheFileIsEmpty()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);

        // Act
        var metadata = await sut.ReadAsync([], PhotographContentTypeConstants.Jpeg, CancellationToken.None);

        // Assert
        metadata.Should().Be(PhotoMetadataModel.Empty);
        await time.DidNotReceive().FromDateTimeLocalValueAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheContentTypeIsNotSupported()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" } });

        // Act
        var metadata = await sut.ReadAsync(bytes, "image/heic", CancellationToken.None);

        // Assert
        metadata.Should().Be(PhotoMetadataModel.Empty);
        await time.DidNotReceive().FromDateTimeLocalValueAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheJpegHasNoExifSegment()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);

        // Act
        var metadata = await sut.ReadAsync(
            JpegWithoutExif(),
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.Should().Be(PhotoMetadataModel.Empty);
        await time.DidNotReceive().FromDateTimeLocalValueAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheExifBlockIsMalformed()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" } });
        for (var index = 12; index < bytes.Length - 2; index++)
        {
            bytes[index] = 0xAB;
        }

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.Should().Be(PhotoMetadataModel.Empty);
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheExifBlockIsTruncated()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { Latitude = 53.2707, Longitude = -9.0568 });

        // Act
        var metadata = await sut.ReadAsync(
            bytes[..(bytes.Length / 2)],
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.HasCoordinates.Should().BeFalse();
    }

    [Theory]
    [InlineData("    :  :     :  :  ")]
    [InlineData("0000:00:00 00:00:00")]
    [InlineData("1899:12:31 23:59:59")]
    [InlineData("not a date")]
    public async Task ItShouldRejectAnUntrustworthyCaptureDate(string dateTimeOriginal)
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = dateTimeOriginal } });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().BeNull();
        await time.DidNotReceive().FromDateTimeLocalValueAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNoCaptureDateWhenTheBrowserCannotResolveTheWallClock()
    {
        // Arrange
        var time = UnavailableBrowserTime();
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" } });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().BeNull();
        await time.Received(1).FromDateTimeLocalValueAsync(
            "2025-06-14T07:32:10",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectCoordinatesWhenARationalDenominatorIsZero()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent
        {
            Latitude = 53.2707,
            Longitude = -9.0568,
            ZeroDenominator = true
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.HasCoordinates.Should().BeFalse();
    }

    [Theory]
    [InlineData("X", "E")]
    [InlineData("N", "Q")]
    [InlineData("", "E")]
    public async Task ItShouldRejectCoordinatesWithAnUnknownHemisphereReference(
        string latitudeRef,
        string longitudeRef)
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent
        {
            Latitude = 53.2707,
            Longitude = 9.0568,
            LatitudeRef = latitudeRef,
            LongitudeRef = longitudeRef
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.HasCoordinates.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRejectCoordinatesOutsideTheValidRange()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { Latitude = 91.5, Longitude = 9.0568 });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.HasCoordinates.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRejectTheNullIslandSentinelCoordinates()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { Latitude = 0, Longitude = 0 });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.HasCoordinates.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldReturnTheCaptureDateWhenThePhotographHasNoCoordinates()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" } });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T06:32:10Z"));
        metadata.HasCoordinates.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldReturnTheCoordinatesWhenThePhotographHasNoCaptureDate()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(1));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { Latitude = 53.2707, Longitude = -9.0568 });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().BeNull();
        metadata.Latitude.Should().BeApproximately(53.2707, 0.0001);
        metadata.Longitude.Should().BeApproximately(-9.0568, 0.0001);
        await time.DidNotReceive().FromDateTimeLocalValueAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldResolveTheCaptureWallClockThroughTheBrowserWhenExifHasNoOffset()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(2));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" } });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T05:32:10Z"));
        await time.Received(1).FromDateTimeLocalValueAsync(
            "2025-06-14T07:32:10",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseTheExifOffsetWithoutAskingTheBrowserWhenOneIsRecorded()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.FromHours(5));
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = "2025:06:14 07:32:10",
                [OffsetTimeOriginalTag] = "+01:00"
            }
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T06:32:10Z"));
        await time.DidNotReceive().FromDateTimeLocalValueAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFallBackToTheDigitizedDateWhenNoOriginalDateExists()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.Zero);
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeDigitizedTag] = "2024:09:01 18:05:00",
                [OffsetTimeDigitizedTag] = "+03:00"
            }
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2024-09-01T15:05:00Z"));
    }

    [Fact]
    public async Task ItShouldPreferTheOriginalDateOverTheDigitizedDate()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.Zero);
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = "2024:09:01 18:05:00",
                [DateTimeDigitizedTag] = "2026:01:02 09:00:00"
            }
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2024-09-01T18:05:00Z"));
        await time.Received(1).FromDateTimeLocalValueAsync(
            "2024-09-01T18:05:00",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReadSouthernAndWesternCoordinatesAsNegative()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.Zero);
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(new ExifContent { Latitude = -33.8688, Longitude = -70.6693 });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.Latitude.Should().BeApproximately(-33.8688, 0.0001);
        metadata.Longitude.Should().BeApproximately(-70.6693, 0.0001);
    }

    [Fact]
    public async Task ItShouldReadBigEndianExif()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.Zero);
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(
            new ExifContent
            {
                ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" },
                Latitude = 53.2707,
                Longitude = -9.0568
            },
            bigEndian: true);

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T07:32:10Z"));
        metadata.Latitude.Should().BeApproximately(53.2707, 0.0001);
        metadata.Longitude.Should().BeApproximately(-9.0568, 0.0001);
    }

    [Fact]
    public async Task ItShouldReadExifFromAJpegWithoutAJfifSegment()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.Zero);
        var sut = new PhotoMetadataService(time);
        var bytes = Jpeg(
            new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" } },
            withJfifSegment: false);

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T07:32:10Z"));
    }

    [Fact]
    public async Task ItShouldReadExifFromAPngChunk()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.Zero);
        var sut = new PhotoMetadataService(time);
        var bytes = Png(new ExifContent
        {
            ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" },
            Latitude = 53.2707,
            Longitude = -9.0568
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Png,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T07:32:10Z"));
        metadata.Latitude.Should().BeApproximately(53.2707, 0.0001);
    }

    [Fact]
    public async Task ItShouldReadExifFromAWebpChunk()
    {
        // Arrange
        var time = BrowserTime(TimeSpan.Zero);
        var sut = new PhotoMetadataService(time);
        var bytes = Webp(new ExifContent
        {
            ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" },
            Latitude = 53.2707,
            Longitude = -9.0568
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Webp,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T07:32:10Z"));
        metadata.Longitude.Should().BeApproximately(-9.0568, 0.0001);
    }
}
