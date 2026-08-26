using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using PhotographPickerComponent =
    FishingLogBook.Web.Features.Photographs.Components.PhotographPicker.PhotographPicker;

namespace FishingLogBook.Web.Tests.Features.Photographs.Components.PhotographPickerTests;

public class WhenTestingSelection : BasePhotographPickerTest
{
    private const byte FirstPhotograph = 0x0A;
    private const byte SecondPhotograph = 0x0B;
    private const byte UnsupportedPhotograph = 0x0C;
    private const byte UnpreparablePhotograph = 0x0D;

    private static readonly Guid FirstId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid SecondId = Guid.Parse("11111111-0000-0000-0000-000000000002");

    [Fact]
    public async Task ItShouldReportAnUnsupportedFormatWithoutRaisingPreparedPhotographs()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preparation = PreparationFor((UnsupportedPhotograph, PhotographPreparationModel.Unsupported));
        await using var context = CreateContext(preparation);
        var prepared = new List<IReadOnlyList<PreparedPhotographModel>>();
        var cut = context.Render<PhotographPickerComponent>(parameters => parameters
            .Add(picker => picker.IdPrefix, "catch")
            .Add(picker => picker.PhotographsPrepared, EventCallback.Factory.Create<IReadOnlyList<PreparedPhotographModel>>(
                this,
                photographs => prepared.Add(photographs))));

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("bad.heic", UnsupportedPhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-unsupported").TextContent.Should().Contain("isn't supported"));
        cut.FindAll("#catch-photo-unpreparable").Should().BeEmpty();
        prepared.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReportAPreparationFailureWithoutRaisingPreparedPhotographs()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preparation = PreparationFor(
            (UnpreparablePhotograph, PhotographPreparationModel.CouldNotPrepare));
        await using var context = CreateContext(preparation);
        var prepared = new List<IReadOnlyList<PreparedPhotographModel>>();
        var cut = context.Render<PhotographPickerComponent>(parameters => parameters
            .Add(picker => picker.IdPrefix, "catch")
            .Add(picker => picker.PhotographsPrepared, EventCallback.Factory.Create<IReadOnlyList<PreparedPhotographModel>>(
                this,
                photographs => prepared.Add(photographs))));

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(JpegFile("broken.jpg", UnpreparablePhotograph));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-photo-unpreparable").TextContent.Should().Contain("could not be prepared"));
        cut.FindAll("#catch-photo-unsupported").Should().BeEmpty();
        prepared.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldPrepareACameraCaptureThroughTheSharedPipeline()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preparation = PreparationFor(
            (FirstPhotograph, Prepared(FirstId, [0xFF, 0xD8], PhotographSourceEnum.Camera)));
        await using var context = CreateContext(preparation);
        var prepared = new List<IReadOnlyList<PreparedPhotographModel>>();
        var cut = context.Render<PhotographPickerComponent>(parameters => parameters
            .Add(picker => picker.IdPrefix, "catch")
            .Add(picker => picker.PhotographsPrepared, EventCallback.Factory.Create<IReadOnlyList<PreparedPhotographModel>>(
                this,
                photographs => prepared.Add(photographs))));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("now.jpg", FirstPhotograph));

        // Assert
        cut.WaitForAssertion(() => prepared.Should().HaveCount(1));
        prepared[0].Should().HaveCount(1);
        prepared[0][0].Source.Should().Be(PhotographSourceEnum.Camera);
        await preparation.Received(1).PrepareAsync(
            Arg.Any<IBrowserFile>(),
            PhotographSourceEnum.Camera,
            Arg.Any<CancellationToken>());
        await preparation.DidNotReceive().PrepareAsync(
            Arg.Any<IBrowserFile>(),
            PhotographSourceEnum.Gallery,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRaiseEverySanitisedGalleryPhotographAndKeepReportingRejections()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var firstBytes = new byte[] { 0xFF, 0xD8, 0x01 };
        var secondBytes = new byte[] { 0xFF, 0xD8, 0x02 };
        var preparation = PreparationFor(
            (FirstPhotograph, Prepared(FirstId, firstBytes, capturedOnLocal: "2025-06-14T07:32")),
            (SecondPhotograph, Prepared(SecondId, secondBytes)),
            (UnsupportedPhotograph, PhotographPreparationModel.Unsupported));
        await using var context = CreateContext(preparation);
        var prepared = new List<IReadOnlyList<PreparedPhotographModel>>();
        var cut = context.Render<PhotographPickerComponent>(parameters => parameters
            .Add(picker => picker.IdPrefix, "catch")
            .Add(picker => picker.PhotographsPrepared, EventCallback.Factory.Create<IReadOnlyList<PreparedPhotographModel>>(
                this,
                photographs => prepared.Add(photographs))));

        // Act
        cut.FindComponents<InputFile>()[1].UploadFiles(
            JpegFile("a.jpg", FirstPhotograph),
            JpegFile("b.jpg", SecondPhotograph),
            JpegFile("c.heic", UnsupportedPhotograph));

        // Assert
        cut.WaitForAssertion(() => prepared.Should().HaveCount(1));
        prepared[0].Should().HaveCount(2);
        prepared[0][0].Id.Should().Be(FirstId);
        prepared[0][0].Bytes.Should().Equal(firstBytes);
        prepared[0][0].CapturedOnLocal.Should().Be("2025-06-14T07:32");
        prepared[0][1].Id.Should().Be(SecondId);
        cut.Find("#catch-photo-unsupported").Should().NotBeNull();
        await preparation.Received(3).PrepareAsync(
            Arg.Any<IBrowserFile>(),
            PhotographSourceEnum.Gallery,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLabelBothInputsAccessiblyAndScopeThemToTheIdPrefix()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preparation = PreparationFor();
        await using var context = CreateContext(preparation);

        // Act
        var cut = context.Render<PhotographPickerComponent>(parameters => parameters
            .Add(picker => picker.IdPrefix, "catch-edit"));

        // Assert
        cut.Find("#catch-edit-photo-camera").GetAttribute("aria-label").Should().Be("Take photo");
        cut.Find("#catch-edit-photo-gallery").GetAttribute("aria-label").Should().Be("Choose photo");
        cut.Find("#catch-edit-photo-camera").GetAttribute("capture").Should().Be("environment");
        cut.Find("#catch-edit-photo-gallery").HasAttribute("multiple").Should().BeTrue();
        cut.Find("label[for=catch-edit-photo-camera]").Id.Should().Be("catch-edit-take-photo");
        cut.Find("label[for=catch-edit-photo-gallery]").Id.Should().Be("catch-edit-choose-photo");
        await preparation.DidNotReceive().PrepareAsync(
            Arg.Any<IBrowserFile>(),
            Arg.Any<PhotographSourceEnum>(),
            Arg.Any<CancellationToken>());
    }
}
