using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingMixedPhotoSources : BaseRecordCatchTest
{
    private const byte CameraPhotograph = 0x0A;
    private const byte JunePhotograph = 0x0B;
    private const byte MayPhotograph = 0x0C;

    private static readonly DateTimeOffset JuneCapture = DateTimeOffset.Parse("2025-06-14T06:32:10Z");
    private static readonly DateTimeOffset MayCapture = DateTimeOffset.Parse("2025-05-02T14:10:00Z");

    private const double CorribLatitude = 53.2707;
    private const double CorribLongitude = -9.0568;
    private const double LeeLatitude = 51.8985;
    private const double LeeLongitude = -8.4756;

    [Fact]
    public async Task ItShouldBlockSaveUntilTheAnglerResolvesAConflictBetweenTwoGalleryPhotographsWhileACameraPhotographIsPresent()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (CameraPhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (JunePhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (MayPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("now.jpg", CameraPhotograph));

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", JunePhotograph),
            JpegFile("may.jpg", MayPhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull());
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());

        await cut.Find("#catch-photo-use-details").ClickAsync();
        cut.Find("#catch-photo-metadata-conflict").Should().NotBeNull();
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 3
                && catchRecord.CaughtOn == MayCapture
                && catchRecord.Location!.Latitude == LeeLatitude
                && catchRecord.Location.Longitude == LeeLongitude),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldProposeTheGalleryPhotographsDetailsWhenOnlyOneGalleryPhotographHasMetadata()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (CameraPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)),
            (JunePhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("now.jpg", CameraPhotograph));

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("june.jpg", JunePhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-caught-on").GetAttribute("value").Should().Be("2025-06-14T06:32"));
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        cut.Find("#catch-location-from-photo").Should().NotBeNull();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 2
                && catchRecord.CaughtOn == JuneCapture
                && catchRecord.Location!.Latitude == CorribLatitude
                && catchRecord.Location.Longitude == CorribLongitude
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotFabricateADateOrLocationWhenNeitherPhotographHasMetadata()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("now.jpg", CameraPhotograph));

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("plain.jpg", JunePhotograph));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-photo-carousel").Should().NotBeNull());
        cut.FindAll("#catch-photo-metadata-conflict").Should().BeEmpty();
        cut.FindAll("#catch-location-from-photo").Should().BeEmpty();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 2
                && catchRecord.CaughtOn > DateTimeOffset.UtcNow.AddMinutes(-5)
                && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecomputeAfterTheConflictingGalleryPhotographIsRemovedWhileACameraPhotographRemains()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var photoMetadata = PhotoMetadataFor(
            (JunePhotograph, new PhotographMetadataModel(JuneCapture, CorribLatitude, CorribLongitude)),
            (MayPhotograph, new PhotographMetadataModel(MayCapture, LeeLatitude, LeeLongitude)));
        await using var context = CreateContext(store, photoMetadata: photoMetadata);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("now.jpg", CameraPhotograph));
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("june.jpg", JunePhotograph),
            JpegFile("may.jpg", MayPhotograph));
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
                catchRecord.Photographs.Count == 2
                && catchRecord.CaughtOn == JuneCapture
                && catchRecord.Location!.Latitude == CorribLatitude
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata),
            Arg.Any<CancellationToken>());
    }
}
