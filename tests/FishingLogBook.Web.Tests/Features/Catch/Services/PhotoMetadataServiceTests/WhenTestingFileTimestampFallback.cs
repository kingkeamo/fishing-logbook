using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Enums;
using FishingLogBook.Web.Features.Catch.Services;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.PhotoMetadataServiceTests;

public class WhenTestingFileTimestampFallback : BasePhotoMetadataServiceTest
{
    private static readonly DateTimeOffset FileModified =
        DateTimeOffset.Parse("2026-08-22T10:28:43+00:00");

    [Fact]
    public async Task ItShouldNotProposeADateWhenNoMetadataAndNoFileTimestampExist()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));

        // Act
        var metadata = await sut.ReadAsync(
            JpegWithoutExif(),
            PhotographContentTypeConstants.Jpeg,
            null,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().BeNull();
        metadata.CapturedOnSource.Should().Be(PhotoCapturedOnSourceEnum.None);
    }

    [Theory]
    [InlineData("0001-01-01T00:00:00+00:00")]
    [InlineData("1899-12-31T23:59:59+00:00")]
    public async Task ItShouldRejectAnUnusableFileTimestamp(string lastModified)
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));

        // Act
        var metadata = await sut.ReadAsync(
            JpegWithoutExif(),
            PhotographContentTypeConstants.Jpeg,
            DateTimeOffset.Parse(lastModified),
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().BeNull();
        metadata.CapturedOnSource.Should().Be(PhotoCapturedOnSourceEnum.None);
    }

    [Fact]
    public async Task ItShouldUseAPlausibleFileTimestampForAJpegWithoutACaptureDate()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));

        // Act
        var metadata = await sut.ReadAsync(
            JpegWithoutExif(),
            PhotographContentTypeConstants.Jpeg,
            FileModified,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(FileModified);
        metadata.CapturedOnSource.Should().Be(PhotoCapturedOnSourceEnum.FileLastModified);
    }

    [Fact]
    public async Task ItShouldUseAPlausibleFileTimestampForAPngWithoutAnyMetadata()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));

        // Act
        var metadata = await sut.ReadAsync(
            PngWithoutMetadata(),
            PhotographContentTypeConstants.Png,
            FileModified,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(FileModified);
        metadata.CapturedOnSource.Should().Be(PhotoCapturedOnSourceEnum.FileLastModified);
    }

    [Fact]
    public async Task ItShouldUseAPlausibleFileTimestampForAWebpWithoutACaptureDate()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));

        // Act
        var metadata = await sut.ReadAsync(
            Webp(new ExifContent { Latitude = 53.2707, Longitude = -9.0568 }),
            PhotographContentTypeConstants.Webp,
            FileModified,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(FileModified);
        metadata.CapturedOnSource.Should().Be(PhotoCapturedOnSourceEnum.FileLastModified);
        metadata.Latitude.Should().BeApproximately(53.2707, 0.0001);
    }

    [Fact]
    public async Task ItShouldAcceptAnOldButPlausibleFileTimestamp()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var longAgo = DateTimeOffset.Parse("1998-07-04T11:15:00+00:00");

        // Act
        var metadata = await sut.ReadAsync(
            JpegWithoutExif(),
            PhotographContentTypeConstants.Jpeg,
            longAgo,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(longAgo);
        metadata.CapturedOnSource.Should().Be(PhotoCapturedOnSourceEnum.FileLastModified);
    }

    [Fact]
    public async Task ItShouldPreferTheExifCaptureDateOverTheFileTimestamp()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent { ExifText = { [DateTimeOriginalTag] = "2025:06:14 07:32:10" } });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            FileModified,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T07:32:10Z"));
        metadata.CapturedOnSource.Should().Be(PhotoCapturedOnSourceEnum.ExifOriginal);
    }

    [Fact]
    public async Task ItShouldRecordTheDigitizedTagAsItsOwnProvenance()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent { ExifText = { [DateTimeDigitizedTag] = "2025:06:14 07:32:10" } });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            FileModified,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T07:32:10Z"));
        metadata.CapturedOnSource.Should().Be(PhotoCapturedOnSourceEnum.ExifDigitized);
        metadata.HasTrustworthyCapturedOn.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldFallBackToTheFileTimestampWhenTheExifDateIsUnusable()
    {
        // Arrange
        var sut = new PhotoMetadataService(BrowserTime(TimeSpan.Zero));
        var bytes = Jpeg(new ExifContent
        {
            ExifText = { [DateTimeOriginalTag] = "0000:00:00 00:00:00" },
            Latitude = 53.2707,
            Longitude = -9.0568
        });

        // Act
        var metadata = await sut.ReadAsync(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            FileModified,
            CancellationToken.None);

        // Assert
        metadata.CapturedOn.Should().Be(FileModified);
        metadata.CapturedOnSource.Should().Be(PhotoCapturedOnSourceEnum.FileLastModified);
        metadata.HasTrustworthyCapturedOn.Should().BeFalse();
        metadata.Latitude.Should().BeApproximately(53.2707, 0.0001);
    }
}
