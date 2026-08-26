using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Services;

namespace FishingLogBook.Web.Tests.Features.Photographs.Services.PhotographMetadataServiceTests;

public class WhenTestingCaptureTimestampPlausibility : BasePhotographMetadataServiceTest
{
    private const double CorribLatitude = 53.2707;
    private const double CorribLongitude = -9.0568;

    [Fact]
    public async Task ItShouldRejectAnExifCaptureBeyondTheAllowedFutureSkew()
    {
        // Arrange
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = "2026:08:26 12:20:00",
                [OffsetTimeOriginalTag] = "+00:00"
            }
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            null,
            ReferenceNow,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().BeNull();
        metadata.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.None);
    }

    [Fact]
    public async Task ItShouldKeepValidCoordinatesWhenTheExifCaptureIsInTheFuture()
    {
        // Arrange
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = "2030:01:01 09:00:00",
                [OffsetTimeOriginalTag] = "+00:00"
            },
            Latitude = CorribLatitude,
            Longitude = CorribLongitude
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            null,
            ReferenceNow,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().BeNull();
        metadata.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.None);
        metadata.HasCoordinates.Should().BeTrue();
        metadata.Latitude.Should().BeApproximately(CorribLatitude, 0.0001);
        metadata.Longitude.Should().BeApproximately(CorribLongitude, 0.0001);
    }

    [Fact]
    public async Task ItShouldFallBackToAPlausibleFileTimestampWhenTheExifCaptureIsInTheFuture()
    {
        // Arrange
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = "2030:01:01 09:00:00",
                [OffsetTimeOriginalTag] = "+00:00"
            }
        });
        var lastModified = DateTimeOffset.Parse("2026-08-22T10:28:43+00:00");

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            lastModified,
            ReferenceNow,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(lastModified);
        metadata.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.FileLastModified);
        metadata.HasTrustworthyCapturedOn.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldFallBackToTheDigitizedDateWhenTheOriginalIsInTheFuture()
    {
        // Arrange
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = "2030:01:01 09:00:00",
                [OffsetTimeOriginalTag] = "+00:00",
                [DateTimeDigitizedTag] = "2025:06:14 07:32:10",
                [OffsetTimeDigitizedTag] = "+00:00"
            }
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            null,
            ReferenceNow,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T07:32:10Z"));
        metadata.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.ExifDigitized);
        metadata.HasTrustworthyCapturedOn.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRejectAFileTimestampBeyondTheAllowedFutureSkew()
    {
        // Arrange
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.Zero));

        // Act
        var metadata = await sut.ReadAsync(
            JpegWithoutExif(),
            PhotographContentTypeConstants.Jpeg,
            ReferenceNow.AddMinutes(16),
            ReferenceNow,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().BeNull();
        metadata.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.None);
    }

    [Fact]
    public async Task ItShouldAcceptAFileTimestampWithinTheAllowedFutureSkew()
    {
        // Arrange
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.Zero));
        var lastModified = ReferenceNow.AddMinutes(14);

        // Act
        var metadata = await sut.ReadAsync(
            JpegWithoutExif(),
            PhotographContentTypeConstants.Jpeg,
            lastModified,
            ReferenceNow,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(lastModified);
        metadata.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.FileLastModified);
    }

    [Fact]
    public async Task ItShouldAcceptAnExifCaptureWithinTheAllowedFutureSkew()
    {
        // Arrange
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = "2026:08:26 12:14:00",
                [OffsetTimeOriginalTag] = "+00:00"
            }
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            null,
            ReferenceNow,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2026-08-26T12:14:00Z"));
        metadata.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.ExifOriginal);
        metadata.HasTrustworthyCapturedOn.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepAnOldHistoricalCaptureTimestamp()
    {
        // Arrange
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = "1974:05:02 06:15:00",
                [OffsetTimeOriginalTag] = "+00:00"
            },
            Latitude = CorribLatitude,
            Longitude = CorribLongitude
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            null,
            ReferenceNow,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("1974-05-02T06:15:00Z"));
        metadata.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.ExifOriginal);
        metadata.HasCoordinates.Should().BeTrue();
    }
}
