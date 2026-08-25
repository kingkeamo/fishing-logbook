using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingMeasurements : BaseRecordCatchTest
{
    [Fact]
    public async Task ItShouldShowThePhotographBeforeTheSupportingMeasurements()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-weight").Should().NotBeNull());

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 1, 2, 3));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-photo-carousel").Should().NotBeNull());
        cut.Markup.IndexOf("catch-photo-carousel", StringComparison.Ordinal)
            .Should().BeLessThan(cut.Markup.IndexOf("catch-take-photo", StringComparison.Ordinal));
        cut.Markup.IndexOf("catch-photo-carousel", StringComparison.Ordinal)
            .Should().BeLessThan(cut.Markup.IndexOf("record-catch-weight", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ItShouldSaveWithoutOptionalMeasurements()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-weight-value").TextContent.Should().Contain("Not recorded"));
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 1, 2, 3));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-saved").Should().NotBeNull());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.Weight == null && catchRecord.Length == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveCanonicalWeightAndLengthSelectedThroughTheSharedEditor()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var modal = Substitute.For<IModalService>();
        AnswerMeasurement(modal, true, 3.75m);
        AnswerMeasurement(modal, false, 72m);
        await using var context = CreateContext(store, modalService: modal);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-weight"));
        await cut.Find("#record-catch-weight").ClickAsync();
        await cut.Find("#record-catch-length").ClickAsync();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 1, 2, 3));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-saved").Should().NotBeNull());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.Weight == 3.75m && catchRecord.Length == 72m),
            Arg.Any<CancellationToken>());
        await modal.Received(1).ShowAsync<MeasurementEditorModal,
            MeasurementEditorModel,
            MeasurementEditorResult>(
                Arg.Is<MeasurementEditorModel>(model => model.IsWeight),
                Arg.Any<CancellationToken>());
        await modal.Received(1).ShowAsync<MeasurementEditorModal,
            MeasurementEditorModel,
            MeasurementEditorResult>(
                Arg.Is<MeasurementEditorModel>(model => !model.IsWeight),
                Arg.Any<CancellationToken>());
    }
}
