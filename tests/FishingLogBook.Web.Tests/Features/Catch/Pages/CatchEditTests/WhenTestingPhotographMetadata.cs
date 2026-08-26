using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingPhotographMetadata : BaseCatchEditTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset HistoricCapture = DateTimeOffset.Parse("2025-06-14T06:32:10Z");
    private const double CorribLatitude = 53.2707;
    private const double CorribLongitude = -9.0568;

    private static readonly CatchLocationModel ExistingDeviceLocation = new(
        51.5074,
        -0.1278,
        12,
        DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
        LocationDefaults.DeviceGps,
        LocationDefaults.Private,
        LocationDefaults.ConsentVersion);

    [Fact]
    public async Task ItShouldRenderExistingPhotographsWithoutExtractingAnyMetadata()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(StoredCatch(CatchId));
        var metadata = PassThroughPhotoMetadata();
        await using var context = CreateContext(store, photoMetadata: metadata);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo").Should().NotBeNull());
        cut.FindAll("#catch-edit-photo-current-metadata").Should().BeEmpty();
        cut.Find("#catch-edit-photo-camera").Should().NotBeNull();
        await metadata.DidNotReceive().ReadAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
        metadata.DidNotReceive().Sanitise(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnpreparablePhotographWithoutStoringItsOriginalBytes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(StoredCatch(CatchId));
        var original = new byte[] { 0x0A, 0x45, 0x78, 0x69, 0x66 };
        await using var context = CreateContext(
            store,
            preparation: PreparationFor((0x0A, PhotographPreparationModel.CouldNotPrepare)));
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("broken.jpg", original));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-photo-unpreparable").TextContent.Should().Contain("could not be prepared"));
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStoreOnlyTheSanitisedBytesOfANewlyAddedPhotograph()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(StoredCatch(CatchId));
        var original = new byte[] { 0x0A, 0x45, 0x78, 0x69, 0x66 };
        var sanitised = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        await using var context = CreateContext(
            store,
            preparation: PreparationFor((0x0A, Prepared(sanitised))));
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("historic.jpg", original));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#catch-edit-photo-unpreparable").Should().BeEmpty());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Photographs.Count == 2
                && catchRecord.Photographs[1].Bytes!.SequenceEqual(sanitised)
                && !catchRecord.Photographs[1].Bytes!.SequenceEqual(original)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowMetadataWithoutChangingTheCatchWhenAHistoricalPhotographIsAdded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(StoredCatch(CatchId, location: ExistingDeviceLocation));
        await using var context = CreateContext(
            store,
            preparation: PreparationFor((0x0A, Prepared(
                [0xFF, 0xD8],
                HistoricCapture,
                CorribLatitude,
                CorribLongitude,
                "2025-06-14T06:32"))));
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("historic.jpg", 0x0A));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-photo-current-date").GetAttribute("data-captured-on").Should()
                .Be("2025-06-14T06:32"));
        cut.Find("#catch-edit-photo-current-location").TextContent.Should()
            .Contain("GPS location available");
        cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T08:00");
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn == StoredCaughtOn
                && catchRecord.Location!.Source == LocationDefaults.DeviceGps
                && catchRecord.Location.Latitude == 51.5074),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotChangeTheCatchWhenTheAnglerOnlyMovesBetweenPhotographs()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(StoredCatch(CatchId, location: ExistingDeviceLocation));
        await using var context = CreateContext(
            store,
            preparation: PreparationFor((0x0A, Prepared(
                [0xFF, 0xD8],
                HistoricCapture,
                CorribLatitude,
                CorribLongitude,
                "2025-06-14T06:32"))));
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("historic.jpg", 0x0A));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-current-metadata").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-photo-previous").ClickAsync();

        // Assert
        cut.FindAll("#catch-edit-photo-current-metadata").Should().BeEmpty();
        cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T08:00");
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.CaughtOn == StoredCaughtOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldApplyOnlyTheDateWhenTheChosenPhotographHasNoCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(StoredCatch(CatchId, location: ExistingDeviceLocation));
        await using var context = CreateContext(
            store,
            preparation: PreparationFor((0x0A, Prepared(
                [0xFF, 0xD8],
                HistoricCapture,
                null,
                null,
                "2025-06-14T06:32"))));
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("historic.jpg", 0x0A));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-use-details").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-photo-use-details").ClickAsync();
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-caught-on").GetAttribute("value").Should()
            .Be("2025-06-14T06:32"));
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn == HistoricCapture
                && catchRecord.Location!.Source == LocationDefaults.DeviceGps
                && catchRecord.Location.Latitude == 51.5074),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldApplyOnlyTheCoordinatesWhenTheChosenPhotographHasNoDate()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(StoredCatch(CatchId));
        await using var context = CreateContext(
            store,
            preparation: PreparationFor((0x0A, Prepared(
                [0xFF, 0xD8],
                null,
                CorribLatitude,
                CorribLongitude,
                null))));
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("located.jpg", 0x0A));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-use-details").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-photo-use-details").ClickAsync();
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T08:00");
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn == StoredCaughtOn
                && catchRecord.Location!.Latitude == CorribLatitude
                && catchRecord.Location.Longitude == CorribLongitude
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata
                && catchRecord.Location.Visibility == LocationDefaults.Private
                && catchRecord.MetadataSyncStatus == SyncStatus.WaitingToSynchronise),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplaceAnExistingDeviceLocationOnlyWhenTheAnglerAppliesThePhotograph()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(StoredCatch(CatchId, location: ExistingDeviceLocation));
        await using var context = CreateContext(
            store,
            preparation: PreparationFor((0x0A, Prepared(
                [0xFF, 0xD8],
                HistoricCapture,
                CorribLatitude,
                CorribLongitude,
                "2025-06-14T06:32"))));
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("historic.jpg", 0x0A));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-use-details").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-photo-use-details").ClickAsync();
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-caught-on").GetAttribute("value").Should()
            .Be("2025-06-14T06:32"));
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn == HistoricCapture
                && catchRecord.Location!.Latitude == CorribLatitude
                && catchRecord.Location.Source == LocationDefaults.PhotoMetadata
                && catchRecord.Location.CapturedOn == HistoricCapture),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOfferTheActionForAPhotographWithNeitherDateNorCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWith(StoredCatch(CatchId, location: ExistingDeviceLocation));
        await using var context = CreateContext(
            store,
            preparation: PreparationFor((0x0A, Prepared([0xFF, 0xD8], null, null, null, null))));
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-camera").Should().NotBeNull());
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("plain.jpg", 0x0A));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-photo-use-details").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-photo-use-details").ClickAsync();
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.Find("#catch-edit-photo-use-details").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T08:00");
        await store.DidNotReceive().SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.CaughtOn != StoredCaughtOn
                || catchRecord.Location!.Source != LocationDefaults.DeviceGps),
            Arg.Any<CancellationToken>());
    }

    private static ICatchStore StoreWith(CatchModel stored)
    {
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>()).Returns(stored);
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return store;
    }

    private static IPhotographPreparationService PreparationFor(
        params (byte Marker, PhotographPreparationModel Result)[] outcomes)
    {
        var preparation = Substitute.For<IPhotographPreparationService>();
        preparation.PrepareAsync(
                Arg.Any<IBrowserFile>(),
                Arg.Any<PhotographSourceEnum>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var file = call.ArgAt<IBrowserFile>(0);
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                var bytes = buffer.ToArray();
                var match = outcomes.FirstOrDefault(outcome =>
                    bytes.Length > 0 && bytes[0] == outcome.Marker);
                return match.Result ?? PhotographPreparationModel.CouldNotPrepare;
            });
        return preparation;
    }

    private static PhotographPreparationModel Prepared(
        byte[] sanitised,
        DateTimeOffset? capturedOn = null,
        double? latitude = null,
        double? longitude = null,
        string? capturedOnLocal = null)
    {
        return PhotographPreparationModel.Prepared(new PreparedPhotographModel(
            Guid.NewGuid(),
            PhotographContentTypeConstants.Jpeg,
            sanitised,
            PhotographSourceEnum.Gallery,
            new PhotographMetadataModel(
                capturedOn,
                latitude,
                longitude,
                capturedOn is null
                    ? PhotographCapturedOnSourceEnum.None
                    : PhotographCapturedOnSourceEnum.ExifOriginal),
            capturedOnLocal));
    }

    private static InputFileContent JpegFile(string name, params byte[] bytes)
    {
        return InputFileContent.CreateFromBinary(bytes, name, contentType: PhotographContentTypeConstants.Jpeg);
    }
}
