using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingPhotograph : BaseRecordCatchTest
{
    [Fact]
    public async Task ItShouldRejectAnUnsupportedPhotographAndKeepSaveDisabled()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            PhotographFile("catch.heic", "image/heic", 0x00, 0x01, 0x02));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-unsupported").TextContent.Should().Contain("This photo format isn't supported"));
        cut.FindAll("#catch-photo-carousel").Should().BeEmpty();
        cut.FindAll("#catch-caught-on").Should().BeEmpty();
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepAValidPhotographWhenAnUnsupportedFileIsSelected()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("a.jpg", 0xFF, 0xD8, 0xFF));
        var photographId = VisiblePhotographId(cut);
        var caughtOn = cut.Find("#catch-caught-on").TextContent;

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            PhotographFile("b.heic", "image/heic", 0x00, 0x01, 0x02));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-unsupported").TextContent.Should().Contain("This photo format isn't supported"));
        VisiblePhotographId(cut).Should().Be(photographId);
        cut.Find("#catch-photo-position").TextContent.Should().Contain("Photo 1 of 1");
        cut.Find("#catch-caught-on").TextContent.Should().Be(caughtOn);
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse();
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.Photographs.Count == 1
                && catchRecord.Photographs[0].Id == photographId
                && catchRecord.Photographs[0].ContentType == PhotographContentTypeConstants.Jpeg
                && catchRecord.Photographs[0].Bytes!.SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF })
                && catchRecord.Photographs[0].ContentType != "image/heic"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSkipUnsupportedFilesInAMixedGallerySelection()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        var heicBytes = new byte[] { 0x00, 0x01, 0x02 };

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", 0x0A),
            PhotographFile("b.heic", "image/heic", heicBytes),
            PhotographFile("c.png", PhotographContentTypeConstants.Png, 0x0C));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-photo-unsupported").TextContent.Should().Contain("This photo format isn't supported");
            cut.Find("#catch-photo-position").TextContent.Should().Contain("Photo 2 of 2");
        });
        var pngId = VisiblePhotographId(cut);
        await cut.Find("#catch-photo-prev").ClickAsync();
        var jpegId = VisiblePhotographId(cut);
        jpegId.Should().NotBe(pngId);
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.Photographs.Count == 2
                && catchRecord.Photographs[0].Id == jpegId
                && catchRecord.Photographs[1].Id == pngId
                && catchRecord.Photographs[0].Bytes!.SequenceEqual(new byte[] { 0x0A })
                && catchRecord.Photographs[1].Bytes!.SequenceEqual(new byte[] { 0x0C })
                && catchRecord.Photographs[0].ContentType == PhotographContentTypeConstants.Jpeg
                && catchRecord.Photographs[1].ContentType == PhotographContentTypeConstants.Png
                && catchRecord.Photographs.All(photograph => photograph.ContentType != "image/heic")
                && catchRecord.Photographs.All(photograph =>
                    !photograph.Bytes!.SequenceEqual(heicBytes))
                && catchRecord.Photographs.All(photograph =>
                    photograph.ContentType != PhotographContentTypeConstants.Jpeg
                    || photograph.Bytes!.SequenceEqual(new byte[] { 0x0A }))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearTheUnsupportedMessageWhenAValidPhotographIsSelected()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(
            PhotographFile("catch.heic", "image/heic", 0x00));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-unsupported").Should().NotBeNull());

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#catch-photo-unsupported").Should().BeEmpty();
            cut.Find("#catch-photo-carousel").Should().NotBeNull();
            cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeFalse();
        });
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchUnsupportedPhotographCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(
            PhotographFile("prise.heic", "image/heic", 0x00));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-unsupported").TextContent.Should().Contain("n'est pas pris en charge"));
        cut.FindAll("#catch-photo-carousel").Should().BeEmpty();
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDisableSaveAfterRemovingTheOnlyPhotograph()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        cut.WaitForAssertion(() => cut.Find("#catch-photo-remove").Should().NotBeNull());

        // Act
        await cut.Find("#catch-photo-remove").ClickAsync();

        // Assert
        cut.FindAll("#catch-photo-carousel").Should().BeEmpty();
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchPhotoPositionAndRemoveCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", 0x01),
            JpegFile("b.jpg", 0x02));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-photo-position").TextContent.Should().Contain("Photo 2 sur 2");
            cut.Find("#catch-photo-remove").TextContent.Should().Contain("Retirer la photo");
        });
        cut.Find("#catch-photo-prev").GetAttribute("aria-label").Should().Contain("Photo précédente");
        cut.Find("#catch-photo-next").GetAttribute("aria-label").Should().Contain("Photo suivante");
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAddASecondCameraCaptureInsteadOfReplacingTheFirst()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        var camera = cut.FindComponents<InputFile>()[0];

        // Act
        camera.UploadFiles(JpegFile("first.jpg", 0xFF, 0xD8, 0x01));
        var firstId = VisiblePhotographId(cut);
        camera.UploadFiles(PhotographFile("second.jpg", PhotographContentTypeConstants.Png, 0x89, 0x50, 0x4E));
        var secondId = VisiblePhotographId(cut);
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        firstId.Should().NotBe(secondId);
        cut.Find("#catch-photo-position").TextContent.Should().Contain("Photo 2 of 2");
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.Photographs.Count == 2
                && catchRecord.Photographs[0].Id == firstId
                && catchRecord.Photographs[1].Id == secondId
                && catchRecord.Photographs[0].Bytes!.SequenceEqual(new byte[] { 0xFF, 0xD8, 0x01 })
                && catchRecord.Photographs[1].Bytes!.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E })
                && catchRecord.Photographs[0].ContentType == PhotographContentTypeConstants.Jpeg
                && catchRecord.Photographs[1].ContentType == PhotographContentTypeConstants.Png
                && catchRecord.Photographs.All(photograph => photograph.CatchId == catchRecord.Id)
                && catchRecord.Photographs.All(photograph => photograph.Id != catchRecord.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAddEveryFileFromOneGallerySelection()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.Find("#catch-photo-gallery").HasAttribute("multiple").Should().BeTrue();
        cut.Find("#catch-photo-camera").HasAttribute("multiple").Should().BeFalse();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", 0x01),
            PhotographFile("b.png", PhotographContentTypeConstants.Png, 0x02),
            PhotographFile("c.webp", PhotographContentTypeConstants.Webp, 0x03));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-photo-carousel").Should().NotBeNull();
            cut.Find("#catch-photo-position").TextContent.Should().Contain("Photo 3 of 3");
        });
        var thirdId = VisiblePhotographId(cut);
        await cut.Find("#catch-photo-prev").ClickAsync();
        var secondId = VisiblePhotographId(cut);
        await cut.Find("#catch-photo-prev").ClickAsync();
        var firstId = VisiblePhotographId(cut);
        new[] { firstId, secondId, thirdId }.Should().OnlyHaveUniqueItems();
        cut.Find("#catch-photo-position").TextContent.Should().Contain("Photo 1 of 3");
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.Photographs.Count == 3
                && catchRecord.Photographs[0].Id == firstId
                && catchRecord.Photographs[1].Id == secondId
                && catchRecord.Photographs[2].Id == thirdId
                && catchRecord.Photographs[0].Bytes!.SequenceEqual(new byte[] { 0x01 })
                && catchRecord.Photographs[1].Bytes!.SequenceEqual(new byte[] { 0x02 })
                && catchRecord.Photographs[2].Bytes!.SequenceEqual(new byte[] { 0x03 })
                && catchRecord.Photographs[0].ContentType == PhotographContentTypeConstants.Jpeg
                && catchRecord.Photographs[1].ContentType == PhotographContentTypeConstants.Png
                && catchRecord.Photographs[2].ContentType == PhotographContentTypeConstants.Webp
                && catchRecord.Photographs.Select(photograph => photograph.Id).Distinct().Count() == 3
                && catchRecord.Photographs.All(photograph => photograph.Id != Guid.Empty)
                && catchRecord.Photographs.All(photograph => photograph.CatchId == catchRecord.Id)
                && catchRecord.Photographs.All(photograph => photograph.Id != catchRecord.Id)),
            Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-saved").Should().NotBeNull();
            cut.Find("#catch-photo-carousel").Should().NotBeNull();
            cut.Find("#catch-photo-position").TextContent.Should().Contain("Photo 1 of 3");
            cut.Find("#catch-record-another").Should().NotBeNull();
        });
        cut.FindAll("#catch-photo-remove").Should().BeEmpty();
        cut.FindAll("#catch-take-photo").Should().BeEmpty();
        cut.FindAll("#catch-choose-photo").Should().BeEmpty();
        cut.FindAll("#save-catch-button").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNavigateTheCarouselWithoutChangingPhotographIds()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", 0x01),
            JpegFile("b.jpg", 0x02),
            JpegFile("c.jpg", 0x03));
        var thirdId = VisiblePhotographId(cut);
        await cut.Find("#catch-photo-prev").ClickAsync();
        var secondId = VisiblePhotographId(cut);
        await cut.Find("#catch-photo-prev").ClickAsync();
        var firstId = VisiblePhotographId(cut);
        await cut.Find("#catch-photo-next").ClickAsync();

        // Assert
        VisiblePhotographId(cut).Should().Be(secondId);
        cut.Find("#catch-photo-position").TextContent.Should().Contain("Photo 2 of 3");
        new[] { firstId, secondId, thirdId }.Should().OnlyHaveUniqueItems();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemoveTheVisiblePhotographByIdAndSaveTheRemainder()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", 0x0A),
            JpegFile("b.jpg", 0x0B),
            JpegFile("c.jpg", 0x0C));
        var photoC = VisiblePhotographId(cut);
        await cut.Find("#catch-photo-prev").ClickAsync();
        var photoB = VisiblePhotographId(cut);
        await cut.Find("#catch-photo-prev").ClickAsync();
        var photoA = VisiblePhotographId(cut);
        await cut.Find("#catch-photo-next").ClickAsync();
        VisiblePhotographId(cut).Should().Be(photoB);
        cut.Find("#catch-photo-position").TextContent.Should().Contain("Photo 2 of 3");

        // Act
        await cut.Find("#catch-photo-remove").ClickAsync();

        // Assert
        cut.Find("#catch-photo-position").TextContent.Should().Contain(" of 2");
        var remainingVisible = VisiblePhotographId(cut);
        remainingVisible.Should().Be(photoC);
        remainingVisible.Should().NotBe(photoB);
        await cut.Find("#save-catch-button").ClickAsync();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.Photographs.Count == 2
                && catchRecord.Photographs.Select(photograph => photograph.Id).Contains(photoA)
                && catchRecord.Photographs.Select(photograph => photograph.Id).Contains(photoC)
                && !catchRecord.Photographs.Select(photograph => photograph.Id).Contains(photoB)
                && catchRecord.Photographs.All(photograph => photograph.CatchId == catchRecord.Id)),
            Arg.Any<CancellationToken>());
    }
}
