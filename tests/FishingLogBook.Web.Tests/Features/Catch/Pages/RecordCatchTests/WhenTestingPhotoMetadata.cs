using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
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

public class WhenTestingPhotoMetadata : BaseRecordCatchTest
{
    private const byte FirstPhotograph = 0x0A;
    private const byte SecondPhotograph = 0x0B;
    private const byte ThirdPhotograph = 0x0C;

    private static readonly DateTimeOffset HistoricCapture =
        DateTimeOffset.Parse("2025-06-14T06:32:10Z");

    [Fact]
    public async Task ItShouldStillRecordThePhotographAndLogSafelyWhenMetadataExtractionFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = Substitute.For<IPhotographMetadataService>();
        photoMetadata.ReadAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns<Task<PhotographMetadataModel>>(_ =>
                throw new InvalidOperationException("EXIF GPS 53.2707,-9.0568 at offset 42 in beach.jpg"));
        PassThroughSanitisation(photoMetadata);
        var logging = QuietLogging();
        await using var context = CreateContext(store, logging: logging, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("beach.jpg", FirstPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().NotBeNullOrWhiteSpace());
        cut.FindAll("#catch-photo-unpreparable").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 1
                && catchRecord.Location == null
                && catchRecord.CaughtOn > DateTimeOffset.UtcNow.AddMinutes(-5)),
            Arg.Any<CancellationToken>());
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
    public async Task ItShouldNotAddAPhotographThatCannotHaveItsMetadataRemoved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var photoMetadata = Substitute.For<IPhotographMetadataService>();
        photoMetadata.ReadAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(PhotographMetadataModel.Empty);
        photoMetadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns<byte[]?>(_ => throw new InvalidOperationException("GPS 53.2707 at offset 42"));
        var logging = QuietLogging();
        await using var context = CreateContext(store, logging: logging, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("a.jpg", FirstPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-unpreparable").TextContent.Should()
                .Contain("could not be prepared"));
        cut.FindAll("#catch-photo-carousel").Should().BeEmpty();
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
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
    public async Task ItShouldPersistTheSanitisedBytesRatherThanTheSelectedOnes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var sanitisedBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var photoMetadata = SanitisingPhotoMetadata(
            new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568),
            sanitisedBytes);
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("a.jpg", FirstPhotograph, SecondPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32"));
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 1
                && catchRecord.Photographs[0].Bytes!.SequenceEqual(sanitisedBytes)
                && !catchRecord.Photographs[0].Bytes!.SequenceEqual(new byte[] { FirstPhotograph, SecondPhotograph })
                && catchRecord.CaughtOn == HistoricCapture
                && catchRecord.Location!.Latitude == 53.2707),
            Arg.Any<CancellationToken>());
        photoMetadata.Received(1).Sanitise(
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { FirstPhotograph, SecondPhotograph })),
            "image/jpeg");
    }

    [Fact]
    public async Task ItShouldRemoveMetadataFromCameraPhotographsToo()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var sanitisedBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var photoMetadata = SanitisingPhotoMetadata(
            new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568),
            sanitisedBytes);
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("now.jpg", FirstPhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse());
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs[0].Bytes!.SequenceEqual(sanitisedBytes)
                && catchRecord.CaughtOn > DateTimeOffset.UtcNow.AddMinutes(-5)
                && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
        photoMetadata.Received(1).Sanitise(
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { FirstPhotograph })),
            "image/jpeg");
        await photoMetadata.DidNotReceive().ReadAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCreateALocationWhenThePhotographHasNoCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, null, null)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("a.jpg", FirstPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32"));
        cut.FindAll("#catch-location-from-photo").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFallBackToTheCurrentTimeWhenNoPhotographCarriesACaptureDate()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = NoPhotoMetadata();
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("a.jpg", FirstPhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse());
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn > DateTimeOffset.UtcNow.AddMinutes(-5)
                && catchRecord.CaughtOn <= DateTimeOffset.UtcNow.AddMinutes(1)),
            Arg.Any<CancellationToken>());
        await photoMetadata.Received(1).ReadAsync(
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { FirstPhotograph })),
            "image/jpeg",
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldWarnAndBlockSaveWhenCaptureDatesMateriallyDisagree()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, null, null)),
            (SecondPhotograph, new PhotographMetadataModel(HistoricCapture.AddDays(-9), null, null)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", FirstPhotograph),
            JpegFile("b.jpg", SecondPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-metadata-conflict").TextContent.Should()
                .Contain("different capture details"));
        cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-05T06:32");
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveOneCatchAfterTheAnglerChoosesTheRepresentativePhotograph()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, null, null)),
            (SecondPhotograph, new PhotographMetadataModel(HistoricCapture.AddDays(-9), null, null)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", FirstPhotograph),
            JpegFile("b.jpg", SecondPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-use-details").Should().NotBeNull());

        // Act
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 2
                && catchRecord.CaughtOn == DateTimeOffset.Parse("2025-06-05T06:32:10Z")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldWarnAndAttachNoLocationWhileConflictingCoordinatesAreUnresolved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = GrantedLocation(SampleLocation());
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568)),
            (SecondPhotograph, new PhotographMetadataModel(HistoricCapture.AddMinutes(2), 51.8985, -8.4756)));
        await using var context = CreateContext(store, location: location, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", FirstPhotograph),
            JpegFile("b.jpg", SecondPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-metadata-conflict").TextContent.Should()
                .Contain("different capture details"));
        cut.FindAll("#catch-location-from-photo").Should().BeEmpty();
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 2 && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
        await location.DidNotReceive().TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAttachNoLocationAtAllWhilePhotographCoordinatesConflict()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = GrantedLocation(SampleLocation(51.5074, -0.1278));
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, PhotographMetadataModel.Empty),
            (SecondPhotograph, new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568)),
            (ThirdPhotograph, new PhotographMetadataModel(HistoricCapture.AddMinutes(2), 51.8985, -8.4756)));
        await using var context = CreateContext(store, location: location, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("a.jpg", FirstPhotograph));
        cut.WaitForAssertion(() => cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse());

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("b.jpg", SecondPhotograph),
            JpegFile("c.jpg", ThirdPhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull());
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 3 && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReadGalleryMetadataAgainAfterTheCameraPhotographIsRemoved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (SecondPhotograph, new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("camera.jpg", FirstPhotograph));
        var cameraPhotographId = VisiblePhotographId(cut);
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("gallery.jpg", SecondPhotograph));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().NotBe("2025-06-14T06:32"));

        // Act
        await cut.Find("#catch-photo-previous").ClickAsync();
        VisiblePhotographId(cut).Should().Be(cameraPhotographId);
        await cut.Find("#catch-photo-remove").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32"));
        cut.Find("#catch-location-from-photo").Should().NotBeNull();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 1
                && catchRecord.CaughtOn == HistoricCapture
                && catchRecord.Location!.Source == LocationDefaults.PhotoMetadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAllowLocationAsAnAnglerRequestWhenPhotographsArePresent()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(
                new LocationPromptStatus(true, false, false),
                new LocationPromptStatus(false, false, true));
        location.TryCaptureAsync(true, Arg.Any<CancellationToken>())
            .Returns(SampleLocation(51.5074, -0.1278));
        location.TryCaptureAsync(false, Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, null, null)));
        await using var context = CreateContext(store, location: location, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("a.jpg", FirstPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-location-allow").Should().NotBeNull());

        // Act
        await cut.Find("#catch-location-allow").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-location-status").Should().NotBeNull());
        cut.WaitForAssertion(() =>
            location.Received(1).TryCaptureAsync(true, Arg.Any<CancellationToken>()));
        await location.DidNotReceive().TryCaptureAsync(false, Arg.Any<CancellationToken>());
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Location!.Source == LocationDefaults.DeviceGps
                && catchRecord.CaughtOn == HistoricCapture),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepCurrentCatchSemanticsForACameraCapture()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("now.jpg", FirstPhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse());
        cut.FindAll("#catch-location-from-photo").Should().BeEmpty();
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn > DateTimeOffset.UtcNow.AddMinutes(-5)
                && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
        await photoMetadata.DidNotReceive().ReadAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReplacePhotographCoordinatesWithTheCurrentDeviceLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = GrantedLocation(SampleLocation(51.5074, -0.1278));
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568)));
        await using var context = CreateContext(store, location: location, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("a.jpg", FirstPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-from-photo").TextContent.Should().Contain("Location from photo"));
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Location!.Latitude == 53.2707
                && catchRecord.Location.Longitude == -9.0568
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata
                && catchRecord.Location.Visibility == LocationDefaults.Private
                && catchRecord.Location.ConsentVersion == LocationDefaults.ConsentVersion
                && catchRecord.Location.CapturedOn == HistoricCapture
                && catchRecord.Location.AccuracyMetres == null),
            Arg.Any<CancellationToken>());
        await location.DidNotReceive().TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplacePhotographCoordinatesOnlyWhenTheAnglerAsksForTheCurrentLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = GrantedLocationOnRequest(SampleLocation(51.5074, -0.1278));
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568)));
        await using var context = CreateContext(store, location: location, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("a.jpg", FirstPhotograph));
        cut.WaitForAssertion(() => cut.Find("#catch-location-use-current").Should().NotBeNull());

        // Act
        await cut.Find("#catch-location-use-current").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-location-from-photo").Should().BeEmpty());
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Location!.Latitude == 51.5074
                && catchRecord.Location.Source == LocationDefaults.DeviceGps),
            Arg.Any<CancellationToken>());
        await location.Received(1).TryCaptureAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheAnglersOwnCaughtOnInsteadOfTheProposedDate()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, null, null)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("a.jpg", FirstPhotograph));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32"));

        // Act
        cut.Find("#catch-caught-on").Input("2025-06-13T19:15");

        // Assert
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn == DateTimeOffset.Parse("2025-06-13T19:15:00Z")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldProposeTheHistoricCaptureDateAndCoordinatesFromOneSelectedPhotograph()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("a.jpg", FirstPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32"));
        cut.Find("#catch-location-from-photo").TextContent.Should().Contain("Location from photo");
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn == HistoricCapture
                && catchRecord.Location!.Source == LocationDefaults.PhotoMetadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordOneCatchFromSeveralPhotographsOfTheSameFish()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture.AddSeconds(90), 53.2710, -9.0568)),
            (SecondPhotograph, new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0570)),
            (ThirdPhotograph, PhotographMetadataModel.Empty));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", FirstPhotograph),
            JpegFile("b.jpg", SecondPhotograph),
            JpegFile("c.jpg", ThirdPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32"));
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        cut.Find("#catch-photo-count").TextContent.Should().Contain("Photo 3 of 3");
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 3
                && catchRecord.CaughtOn == HistoricCapture
                && catchRecord.Location!.Latitude == 53.2707
                && catchRecord.Location.Longitude == -9.0570
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseTheOnlyAvailableMetadataWhenOtherPhotographsHaveNone()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, PhotographMetadataModel.Empty),
            (SecondPhotograph, new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568)),
            (ThirdPhotograph, PhotographMetadataModel.Empty));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", FirstPhotograph),
            JpegFile("b.jpg", SecondPhotograph),
            JpegFile("c.jpg", ThirdPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32"));
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 3
                && catchRecord.CaughtOn == HistoricCapture
                && catchRecord.Location!.Latitude == 53.2707
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchPhotographMetadataCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ICatchStore>();
        var photoMetadata = PhotoMetadataFor(
            (FirstPhotograph, new PhotographMetadataModel(HistoricCapture, 53.2707, -9.0568)),
            (SecondPhotograph, new PhotographMetadataModel(HistoricCapture.AddDays(-9), 51.8985, -8.4756)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", FirstPhotograph),
            JpegFile("b.jpg", SecondPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-metadata-conflict").TextContent.Should()
                .Contain("mêmes détails de prise de vue"));
        cut.Find("#catch-photo-use-details").TextContent.Should()
            .Contain("Utiliser les détails de cette photo");
        cut.Find("#catch-photo-current-location").TextContent.Should()
            .Contain("Position GPS disponible");
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldProposeTheFileTimestampWhenAGalleryPhotographHasNoCaptureDate()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var lastModified = DateTimeOffset.Parse("2026-08-22T10:28:43+00:00");
        var photoMetadata = RealPhotoMetadata();
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            PhotographFileModifiedOn(
                "capture_260822_102830.png",
                PhotographContentTypeConstants.Png,
                lastModified,
                MinimalPng()));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2026-08-22T10:28"));
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn == lastModified
                && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotGiveACameraPhotographHistoricalSemanticsFromItsFileTimestamp()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var lastModified = DateTimeOffset.Parse("2026-08-22T10:28:43+00:00");
        var photoMetadata = RealPhotoMetadata();
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            PhotographFileModifiedOn(
                "now.jpg",
                PhotographContentTypeConstants.Jpeg,
                lastModified,
                MinimalJpeg()));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse());
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn > DateTimeOffset.UtcNow.AddMinutes(-5)
                && catchRecord.CaughtOn != lastModified),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.CaughtOn == lastModified),
            Arg.Any<CancellationToken>());
    }
}
