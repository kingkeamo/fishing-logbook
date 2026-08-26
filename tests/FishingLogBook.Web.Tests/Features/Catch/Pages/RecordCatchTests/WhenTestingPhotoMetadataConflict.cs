using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingPhotoMetadataConflict : BaseRecordCatchTest
{
    private const byte FirstPhotograph = 0x0A;
    private const byte SecondPhotograph = 0x0B;
    private const byte ThirdPhotograph = 0x0C;
    private const byte FourthPhotograph = 0x0D;

    private static readonly DateTimeOffset JuneCapture = DateTimeOffset.Parse("2025-06-14T06:32:10Z");
    private static readonly DateTimeOffset MayCapture = DateTimeOffset.Parse("2025-05-02T14:10:00Z");

    private const double CorribLatitude = 53.2707;
    private const double CorribLongitude = -9.0568;
    private const double LeeLatitude = 51.8985;
    private const double LeeLongitude = -8.4756;

    [Fact]
    public async Task ItShouldNotOfferTheChoiceForAPhotographWithNeitherDateNorCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, CorribLatitude, CorribLongitude)),
            (ThirdPhotograph, PhotographMetadataModel.Empty));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph),
            JpegFile("plain.jpg", ThirdPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-current-date").TextContent.Should()
                .Contain("No photo date available"));
        cut.Find("#catch-photo-current-location").TextContent.Should()
            .Contain("No photo location available");
        cut.Find("#catch-photo-use-details").HasAttribute("disabled").Should().BeTrue();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotChangeTheProposedCatchWhenTheAnglerOnlyMovesBetweenPhotographs()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, CorribLatitude, CorribLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-05-02T14:10"));

        // Act
        await cut.Find("#catch-photo-previous").ClickAsync();

        // Assert
        cut.Find("#catch-photo-current-date").GetAttribute("data-captured-on").Should()
            .Be("2025-06-14T06:32");
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-05-02T14:10");
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheCurrentPhotographDetailsAndFollowTheCarousel()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, null, null)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-metadata-conflict").TextContent.Should()
                .Contain("different capture details"));
        cut.Find("#catch-photo-current-date").GetAttribute("data-captured-on").Should()
            .Be("2025-05-02T14:10");
        cut.Find("#catch-photo-current-location").TextContent.Should()
            .Contain("No photo location available");

        // Act
        await cut.Find("#catch-photo-previous").ClickAsync();

        // Assert
        cut.Find("#catch-photo-current-date").GetAttribute("data-captured-on").Should()
            .Be("2025-06-14T06:32");
        cut.Find("#catch-photo-current-location").TextContent.Should()
            .Contain("GPS location available");
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotBorrowCoordinatesFromAnotherPhotographWhenTheChosenPhotographHasNone()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, null, null)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-use-details").Should().NotBeNull());

        // Act
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.FindAll("#catch-location-from-photo").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 2
                && catchRecord.CaughtOn == MayCapture
                && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldApplyTheChosenPhotographsDateAndCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-use-details").Should().NotBeNull());

        // Act
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-05-02T14:10");
        cut.Find("#catch-location-from-photo").TextContent.Should().Contain("Location from photo");
        await cut.Find("#save-catch-button").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#catch-saved").Should().NotBeNull());
        cut.FindAll("#catch-photo-current-metadata").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 2
                && catchRecord.CaughtOn == MayCapture
                && catchRecord.Location!.Latitude == LeeLatitude
                && catchRecord.Location.Longitude == LeeLongitude
                && catchRecord.Location.CapturedOn == MayCapture
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata
                && catchRecord.Location.Visibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepAnExplicitDeviceLocationWhileTheAnglerMovesBetweenPhotographs()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = GrantedLocationOnRequest(SampleLocation(51.5074, -0.1278));
        var photoMetadata = ConflictingCoordinates();
        await using var context = CreateContext(store, location: location, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("corrib.jpg", FirstPhotograph),
            JpegFile("lee.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-location-use-current").Should().NotBeNull());
        await cut.Find("#catch-location-use-current").ClickAsync();

        // Act
        await cut.Find("#catch-photo-previous").ClickAsync();
        await cut.Find("#catch-photo-next").ClickAsync();

        // Assert
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.FindAll("#catch-location-from-photo").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Location!.Latitude == 51.5074
                && catchRecord.Location.Source == LocationDefaults.DeviceGps),
            Arg.Any<CancellationToken>());
        await location.Received(1).TryCaptureAsync(true, Arg.Any<CancellationToken>());
        await location.DidNotReceive().TryCaptureAsync(false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplaceAnExplicitDeviceLocationWhenTheChosenPhotographHasCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = GrantedLocationOnRequest(SampleLocation(51.5074, -0.1278));
        var photoMetadata = ConflictingCoordinates();
        await using var context = CreateContext(store, location: location, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("corrib.jpg", FirstPhotograph),
            JpegFile("lee.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-location-use-current").Should().NotBeNull());
        await cut.Find("#catch-location-use-current").ClickAsync();

        // Act
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.Find("#catch-location-from-photo").TextContent.Should().Contain("Location from photo");
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Location!.Latitude == LeeLatitude
                && catchRecord.Location.Longitude == LeeLongitude
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata),
            Arg.Any<CancellationToken>());
        await location.Received(1).TryCaptureAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepAnExplicitDeviceLocationWhenTheChosenPhotographHasNoCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = GrantedLocationOnRequest(SampleLocation(51.5074, -0.1278));
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, null, null)));
        await using var context = CreateContext(store, location: location, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-location-use-current").Should().NotBeNull());
        await cut.Find("#catch-location-use-current").ClickAsync();
        cut.WaitForAssertion(() => cut.FindAll("#catch-location-from-photo").Should().BeEmpty());

        // Act
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.FindAll("#catch-location-from-photo").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn == MayCapture
                && catchRecord.Location!.Latitude == 51.5074
                && catchRecord.Location.Source == LocationDefaults.DeviceGps),
            Arg.Any<CancellationToken>());
        await location.Received(1).TryCaptureAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheChosenPhotographWhenACameraCaptureJoinsTheCatch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-use-details").Should().NotBeNull());
        await cut.Find("#catch-photo-use-details").ClickAsync();
        cut.Find("#catch-location-from-photo").Should().NotBeNull();
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-05-02T14:10");

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("now.jpg", ThirdPhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull());
        cut.Find("#catch-location-from-photo").Should().NotBeNull();
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-05-02T14:10");
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 3
                && catchRecord.CaughtOn == MayCapture
                && catchRecord.Location != null
                && catchRecord.Location.Latitude == LeeLatitude
                && catchRecord.Location.Longitude == LeeLongitude),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLetTheAnglerChooseADifferentRepresentativePhotographAfterResolving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-use-details").Should().NotBeNull());
        await cut.Find("#catch-photo-use-details").ClickAsync();
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-05-02T14:10");

        // Act
        await cut.Find("#catch-photo-previous").ClickAsync();
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.Find("#catch-photo-current-metadata").Should().NotBeNull();
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32");

        // Act
        await cut.Find("#catch-photo-next").ClickAsync();
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-05-02T14:10");

        // Act
        await cut.Find("#catch-photo-previous").ClickAsync();
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32");
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn == JuneCapture
                && catchRecord.Location!.Latitude == CorribLatitude
                && catchRecord.Location.Longitude == CorribLongitude
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheChosenRepresentativeWhileSwipingWithoutChoosingAgain()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-use-details").Should().NotBeNull());
        await cut.Find("#catch-photo-previous").ClickAsync();
        await cut.Find("#catch-photo-use-details").ClickAsync();
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32");

        // Act
        await cut.Find("#catch-photo-next").ClickAsync();

        // Assert
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32");
        cut.Find("#catch-photo-current-date").GetAttribute("data-captured-on").Should().Be("2025-05-02T14:10");
    }

    [Fact]
    public async Task ItShouldKeepTheWarningVisibleWhileNavigatingFourOrMoreConflictingPhotographs()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)),
            (ThirdPhotograph, new PhotographMetadataModel(MayCapture.AddDays(-20), CorribLatitude, CorribLongitude)),
            (FourthPhotograph, new PhotographMetadataModel(MayCapture.AddDays(-40), LeeLatitude, LeeLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph),
            JpegFile("april.jpg", ThirdPhotograph),
            JpegFile("march.jpg", FourthPhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull());
        await cut.Find("#catch-photo-previous").ClickAsync();
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        await cut.Find("#catch-photo-previous").ClickAsync();
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        await cut.Find("#catch-photo-previous").ClickAsync();
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();

        // Act
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotShowAWarningWhenDatesAreWithinTheSameCatchTolerance()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(JuneCapture.AddMinutes(20), CorribLatitude, CorribLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("first.jpg", FirstPhotograph),
            JpegFile("second.jpg", SecondPhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-photo-carousel").Should().NotBeNull());
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        cut.FindAll("#catch-photo-use-details").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotShowAWarningWhenCoordinatesAreWithinTheSameLocationTolerance()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude + 0.0016, CorribLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("first.jpg", FirstPhotograph),
            JpegFile("second.jpg", SecondPhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-photo-carousel").Should().NotBeNull());
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        cut.FindAll("#catch-photo-use-details").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldHideTheChooserAndWarningAfterASuccessfulSave()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-use-details").Should().NotBeNull());
        await cut.Find("#catch-photo-use-details").ClickAsync();
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-saved").Should().NotBeNull());
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        cut.FindAll("#catch-photo-use-details").Should().BeEmpty();
        await store.Received(1).SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplaceTheChosenPhotographsCoordinatesWhenTheAnglerAsksForTheDeviceLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = GrantedLocationOnRequest(SampleLocation(51.5074, -0.1278));
        var photoMetadata = ConflictingCoordinates();
        await using var context = CreateContext(store, location: location, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("corrib.jpg", FirstPhotograph),
            JpegFile("lee.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-use-details").Should().NotBeNull());
        await cut.Find("#catch-photo-use-details").ClickAsync();
        cut.Find("#catch-location-from-photo").TextContent.Should().Contain("Location from photo");

        // Act
        await cut.Find("#catch-location-use-current").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-location-from-photo").Should().BeEmpty());
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:34");
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Location!.Latitude == 51.5074
                && catchRecord.Location.Source == LocationDefaults.DeviceGps
                && catchRecord.CaughtOn == JuneCapture.AddMinutes(2)),
            Arg.Any<CancellationToken>());
        await location.Received(1).TryCaptureAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepAnExplicitDeviceLocationWhenEveryPhotographIsReplaced()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = GrantedLocationOnRequest(SampleLocation(51.5074, -0.1278));
        var photoMetadata = ConflictingCoordinates();
        await using var context = CreateContext(store, location: location, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("corrib.jpg", FirstPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-location-use-current").Should().NotBeNull());
        await cut.Find("#catch-location-use-current").ClickAsync();
        cut.WaitForAssertion(() => cut.FindAll("#catch-location-from-photo").Should().BeEmpty());

        // Act
        await cut.Find("#catch-photo-remove").ClickAsync();
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("lee.jpg", SecondPhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-photo-carousel").Should().NotBeNull());
        cut.FindAll("#catch-location-from-photo").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 1
                && catchRecord.Location!.Latitude == 51.5074
                && catchRecord.Location.Source == LocationDefaults.DeviceGps),
            Arg.Any<CancellationToken>());
        await location.Received(1).TryCaptureAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecomputeTheProposalWhenTheConflictingPhotographIsRemoved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull());

        // Act
        await cut.Find("#catch-photo-remove").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty());
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32");
        cut.Find("#catch-location-from-photo").Should().NotBeNull();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 1
                && catchRecord.CaughtOn == JuneCapture
                && catchRecord.Location!.Latitude == CorribLatitude
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheCarouselAndMetadataAlignedWhenTheCurrentPhotographIsRemoved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)),
            (ThirdPhotograph, new PhotographMetadataModel(MayCapture.AddDays(-20), null, null)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", FirstPhotograph),
            JpegFile("may.jpg", SecondPhotograph),
            JpegFile("april.jpg", ThirdPhotograph));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-current-date").GetAttribute("data-captured-on").Should()
                .Be("2025-04-12T14:10"));

        // Act
        await cut.Find("#catch-photo-remove").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-photo-count").TextContent.Should().Contain("Photo 2 of 2"));
        cut.Find("#catch-photo-current-date").GetAttribute("data-captured-on").Should()
            .Be("2025-05-02T14:10");
        cut.Find("#catch-photo-current-location").TextContent.Should()
            .Contain("GPS location available");
        CurrentMetadataPhotographId(cut).Should().Be(VisiblePhotographId(cut));
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    private static IPhotographMetadataService ConflictingCoordinates()
    {
        return PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (SecondPhotograph, new PhotographMetadataModel(JuneCapture.AddMinutes(2), LeeLatitude, LeeLongitude)));
    }
}
