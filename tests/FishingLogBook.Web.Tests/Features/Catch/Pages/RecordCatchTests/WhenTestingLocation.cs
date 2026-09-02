using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingLocation : BaseRecordCatchTest
{
    [Fact]
    public async Task ItShouldNotRequestCoordinatesWhenRecordCatchOpens()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var location = GrantedLocation(SampleLocation());
        await using var context = CreateContext(store, location);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#save-catch-button").Should().NotBeNull());
        await location.Received(1).GetPromptStatusAsync(Arg.Any<CancellationToken>());
        await location.DidNotReceive().TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
        cut.FindAll("#catch-location").Should().BeEmpty();
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-status").TextContent.Should().Contain("Location on · Private"));
        cut.FindAll("#catch-location-explainer").Should().BeEmpty();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotStartCaptureWhenThePhotographIsUnsupported()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var location = GrantedLocation(SampleLocation());
        await using var context = CreateContext(store, location);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            PhotographFile("catch.heic", "image/heic", 0x00, 0x01, 0x02));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-unsupported").TextContent.Should().Contain("This photo format isn't supported"));
        await location.DidNotReceive().TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
        cut.FindAll("#catch-photo-carousel").Should().BeEmpty();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotStartASecondCaptureWhenMorePhotographsAreAdded()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var location = GrantedLocation(SampleLocation());
        await using var context = CreateContext(store, location);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("first.jpg", 0xFF, 0xD8, 0x01));
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("second.jpg", 0xFF, 0xD8, 0x02));
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("third.jpg", 0xFF, 0xD8, 0x03));

        // Assert
        await location.Received(1).TryCaptureAsync(false, Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveWithoutLocationWhenPermissionIsDenied()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = DeniedLocation();
        await using var context = CreateContext(store, location);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#catch-location-try-again").Should().NotBeNull());
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await location.DidNotReceive().TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.CaughtByUserId == OwnerUserId && catchRecord.Location == null && catchRecord.Photographs.Count == 1),
            Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-saved").TextContent.Should().Contain("Catch saved on this device");
            cut.Find("#catch-photo-carousel").Should().NotBeNull();
            cut.Find("#catch-record-another").Should().NotBeNull();
        });
        cut.FindAll("#catch-location-saved").Should().BeEmpty();
        cut.FindAll("#catch-location-explainer").Should().BeEmpty();
        cut.FindAll("#catch-location").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldSaveWithoutLocationWhenCaptureFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new LocationPromptStatus(false, false, true));
        location.TryCaptureAsync(false, Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        await using var context = CreateContext(store, location);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await location.Received(1).TryCaptureAsync(false, Arg.Any<CancellationToken>());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.CaughtByUserId == OwnerUserId && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() => cut.Find("#catch-saved").TextContent.Should().Contain("Catch saved on this device"));
        cut.FindAll("#catch-location-saved").Should().BeEmpty();
        cut.Find("#catch-photo-carousel").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldSaveWithoutWaitingWhenLocationCaptureIsStillPending()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = HangingCaptureLocation();
        await using var context = CreateContext(store, location);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        await location.Received(1).TryCaptureAsync(false, Arg.Any<CancellationToken>());

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.CaughtByUserId == OwnerUserId && catchRecord.Location == null && catchRecord.Photographs.Count == 1),
            Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-saved").TextContent.Should().Contain("Catch saved on this device");
            cut.Find("#catch-record-another").Should().NotBeNull();
        });
        cut.FindAll("#catch-location-saved").Should().BeEmpty();
        await location.Received(1).TryCaptureAsync(false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectInvalidCoordinatesAndStillSaveTheCatch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var invalid = SampleLocation(latitude: 91);
        var location = GrantedLocation(invalid);
        await using var context = CreateContext(store, location);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await location.Received(1).TryCaptureAsync(false, Arg.Any<CancellationToken>());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.CaughtByUserId == OwnerUserId && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() => cut.Find("#catch-saved").TextContent.Should().Contain("Catch saved on this device"));
        cut.FindAll("#catch-location-saved").Should().BeEmpty();
        cut.FindAll("#catch-location").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldExplainLocationBeforeRequestingPermission()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var location = PromptLocation();
        await using var context = CreateContext(store, location);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-location-explainer").TextContent.Should()
                .Contain("Location can help remember where a catch was made.");
            cut.Find("#catch-location-explainer").TextContent.Should()
                .Contain("Your exact fishing spot stays private by default.");
            cut.Find("#catch-location-allow").TextContent.Should().Contain("Allow location");
            cut.Find("#catch-location-not-now").TextContent.Should().Contain("Not now");
        });
        await location.DidNotReceive().TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
        cut.FindAll("#catch-location").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowLocationOnAfterAllow()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(
                new LocationPromptStatus(true, false, false),
                new LocationPromptStatus(false, false, true));
        location.TryCaptureAsync(true, Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        await using var context = CreateContext(store, location);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#catch-location-allow").Should().NotBeNull());

        // Act
        await cut.Find("#catch-location-allow").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#catch-location-explainer").Should().BeEmpty();
            cut.Find("#catch-location-status").TextContent.Should().Contain("Location on · Private");
        });
        cut.FindAll("#catch-location").Should().BeEmpty();
        await location.Received(1).TryCaptureAsync(true, Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchLocationOnCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ICatchStore>();
        var location = GrantedLocation(SampleLocation());
        await using var context = CreateContext(store, location);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-status").TextContent.Should().Contain("Localisation activée · Privée"));
        await location.DidNotReceive().TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchLocationExplainerCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ICatchStore>();
        var location = PromptLocation();
        await using var context = CreateContext(store, location);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-location-explainer").TextContent.Should()
                .Contain("La localisation peut aider à se souvenir de l'endroit de la prise.");
            cut.Find("#catch-location-explainer").TextContent.Should()
                .Contain("Votre spot de pêche exact reste privé par défaut.");
            cut.Find("#catch-location-allow").TextContent.Should().Contain("Autoriser la localisation");
            cut.Find("#catch-location-not-now").TextContent.Should().Contain("Pas maintenant");
        });
        await location.DidNotReceive().TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDismissTheExplainerWhenNotNowIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var location = Substitute.For<ILocationService>();
        location.GetPromptStatusAsync(Arg.Any<CancellationToken>())
            .Returns(
                new LocationPromptStatus(true, false, false),
                new LocationPromptStatus(false, true, false));
        location.TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((CatchLocationModel?)null);
        await using var context = CreateContext(store, location);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#catch-location-not-now").Should().NotBeNull());

        // Act
        await cut.Find("#catch-location-not-now").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#catch-location-explainer").Should().BeEmpty();
            cut.Find("#catch-location-try-again").Should().NotBeNull();
        });
        await location.Received(1).DismissPromptAsync(Arg.Any<CancellationToken>());
        await location.DidNotReceive().TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReuseCatchALocationOrCaptureImmediatelyWhenRecordingAnother()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var firstLocation = SampleLocation(53.2707, -9.0568);
        var secondLocation = SampleLocation(53.3498, -6.2603);
        var saved = new List<CatchModel>();
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                saved.Add(call.ArgAt<CatchModel>(0));
                return Task.CompletedTask;
            });
        var location = GrantedLocation(firstLocation, secondLocation);
        await using var context = CreateContext(store, location);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("first.jpg", 0xFF, 0xD8, 0x01));
        await cut.Find("#save-catch-button").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#catch-record-another").Should().NotBeNull());
        await location.Received(1).TryCaptureAsync(false, Arg.Any<CancellationToken>());

        // Act
        await cut.Find("#catch-record-another").ClickAsync();

        // Assert
        await location.Received(1).TryCaptureAsync(false, Arg.Any<CancellationToken>());
        cut.FindAll("#catch-location-saved").Should().BeEmpty();
        cut.Find("#catch-location-status").TextContent.Should().Contain("Location on · Private");
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("second.jpg", 0xFF, 0xD8, 0x02));
        await location.Received(2).TryCaptureAsync(false, Arg.Any<CancellationToken>());
        await cut.Find("#save-catch-button").ClickAsync();
        cut.WaitForAssertion(() => saved.Should().HaveCount(2));
        saved[0].Id.Should().NotBe(saved[1].Id);
        saved[0].Location.Should().Be(firstLocation);
        saved[1].Location.Should().Be(secondLocation);
        saved[1].Location.Should().NotBe(saved[0].Location);
        await store.Received(2).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.CaughtByUserId == OwnerUserId && catchRecord.Location != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistLocationWhenTheFirstValidPhotographStartsCapture()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var captured = SampleLocation();
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var location = GrantedLocation(captured);
        await using var context = CreateContext(store, location);
        var cut = context.Render<RecordCatch>();
        await location.DidNotReceive().TryCaptureAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        await location.Received(1).TryCaptureAsync(false, Arg.Any<CancellationToken>());
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.CaughtByUserId == OwnerUserId &&
                catchRecord.Location == captured
                && catchRecord.Location!.Source == LocationDefaults.DeviceGps
                && catchRecord.Location.Visibility == LocationDefaults.Private
                && catchRecord.Location.ConsentVersion == LocationDefaults.ConsentVersion
                && catchRecord.Photographs.Count == 1),
            Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-saved").TextContent.Should().Contain("Catch saved on this device");
            cut.Find("#catch-location-saved").TextContent.Should().Contain("Location saved");
            cut.Find("#catch-photo-carousel").Should().NotBeNull();
            cut.Find("#catch-record-another").Should().NotBeNull();
        });
        cut.FindAll("#catch-location").Should().BeEmpty();
        cut.FindAll("#catch-location-status").Should().BeEmpty();
        cut.FindAll("#catch-location-privacy-options").Should().BeEmpty();
        cut.FindAll("#catch-location-privacy-save").Should().BeEmpty();
        cut.Markup.Should().NotContain("/location-privacy");
        cut.Find("#catch-location-saved").TextContent.Should().NotContain("53.2707");
        await location.Received(1).TryCaptureAsync(false, Arg.Any<CancellationToken>());
    }
}
