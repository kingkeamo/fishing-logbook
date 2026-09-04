using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Photographs.Services.PhotographMetadataServiceTests;

public class WhenTestingReadHistorical : BasePhotographMetadataServiceTest
{
    [Fact]
    public void ItShouldPreserveAnExplicitOffset()
    {
        // Arrange
        var bytes = Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = "2025:06:14 09:30:00",
                [OffsetTimeOriginalTag] = "+01:00"
            }
        });
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.FromHours(4)));

        // Act
        var result = sut.ReadHistorical(bytes, PhotographContentTypeConstants.Jpeg, null, ReferenceNow);

        // Assert
        result.ExplicitInstant.Should().Be(DateTimeOffset.Parse("2025-06-14T09:30:00+01:00"));
        result.LocalWallClock.Should().BeNull();
        result.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.ExifOriginal);
        result.CapturedOnWasMalformed.Should().BeFalse();
    }

    [Fact]
    public void ItShouldPreserveAnOffsetlessExifValueAsAnAmbiguousWallClock()
    {
        // Arrange
        var bytes = Jpeg(new ExifContent
        {
            ExifText = { [DateTimeOriginalTag] = "2025:06:14 09:30:00" }
        });
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.FromHours(4)));

        // Act
        var result = sut.ReadHistorical(bytes, PhotographContentTypeConstants.Jpeg, null, ReferenceNow);

        // Assert
        result.ExplicitInstant.Should().BeNull();
        result.LocalWallClock.Should().Be(new DateTime(2025, 6, 14, 9, 30, 0));
        result.LocalWallClock!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
        result.CapturedOnWasPresent.Should().BeTrue();
    }

    [Fact]
    public void ItShouldReportMalformedExifWithoutReplacingItWithFileLastModified()
    {
        // Arrange
        var bytes = Jpeg(new ExifContent
        {
            ExifText = { [DateTimeOriginalTag] = "not-a-date" }
        });
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.Zero));

        // Act
        var result = sut.ReadHistorical(
            bytes,
            PhotographContentTypeConstants.Jpeg,
            ReferenceNow.AddDays(-1),
            ReferenceNow);

        // Assert
        result.ExplicitInstant.Should().BeNull();
        result.LocalWallClock.Should().BeNull();
        result.CapturedOnWasPresent.Should().BeTrue();
        result.CapturedOnWasMalformed.Should().BeTrue();
    }

    [Fact]
    public void ItShouldUseFileLastModifiedOnlyWhenExifIsMissing()
    {
        // Arrange
        var fallback = ReferenceNow.AddDays(-1);
        var sut = new PhotographMetadataService(BrowserTime(TimeSpan.Zero));

        // Act
        var result = sut.ReadHistorical(
            JpegWithoutExif(),
            PhotographContentTypeConstants.Jpeg,
            fallback,
            ReferenceNow);

        // Assert
        result.ExplicitInstant.Should().Be(fallback);
        result.CapturedOnSource.Should().Be(PhotographCapturedOnSourceEnum.FileLastModified);
        result.CapturedOnWasPresent.Should().BeFalse();
    }

    [Fact]
    public void ItShouldPreserveOptionalGpsWithoutRequestingBrowserTime()
    {
        // Arrange
        var bytes = Jpeg(new ExifContent { Latitude = 53.3498, Longitude = -6.2603 });
        var time = BrowserTime(TimeSpan.Zero);
        var sut = new PhotographMetadataService(time);

        // Act
        var result = sut.ReadHistorical(bytes, PhotographContentTypeConstants.Jpeg, null, ReferenceNow);

        // Assert
        result.HasCoordinates.Should().BeTrue();
        result.Latitude.Should().BeApproximately(53.3498, 0.0001);
        result.Longitude.Should().BeApproximately(-6.2603, 0.0001);
        time.DidNotReceive().FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
