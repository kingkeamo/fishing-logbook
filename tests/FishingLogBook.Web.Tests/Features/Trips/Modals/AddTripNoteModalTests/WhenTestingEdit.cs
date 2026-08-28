using AngleSharp.Html.Dom;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripNoteModalTests;

public class WhenTestingEdit : BaseAddTripNoteModalTest
{
    [Fact]
    public async Task ItShouldOpenOnTheNoteThatIsBeingEdited()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(WriterThatUpdates());

        // Act
        var (cut, _) = await ShowModalAsync(
            context,
            endedOn: EndedOn,
            existingNote: ExistingNote("water dropped about a foot", StartedOn.AddHours(4)));

        // Assert
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("11:00"));
        DateValue(cut).Should().Be("2026-08-17");
        ((IHtmlTextAreaElement)cut.Find("#trip-note-text")).Value
            .Should().Be("water dropped about a foot");
        cut.Find("#trip-note-modal-title").TextContent.Should().Contain("Edit note");
        cut.Find("#trip-note-save").TextContent.Should().Contain("Save");
    }

    [Fact]
    public async Task ItShouldShowTheFrenchEditCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext(WriterThatUpdates());

        // Act
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn, existingNote: ExistingNote());

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-modal-title").TextContent.Should().Contain("Modifier la note"));
        cut.Find("#trip-note-save").TextContent.Should().Contain("Enregistrer");
    }

    [Fact]
    public async Task ItShouldChangeNothingWhenTheAnglerCancels()
    {
        // Arrange
        var writer = WriterThatUpdates();
        await using var context = CreateContext(writer);
        var (cut, dialog) = await ShowModalAsync(
            context,
            endedOn: EndedOn,
            existingNote: ExistingNote());
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("09:00"));
        cut.Find("#trip-note-text").Input("changed my mind");

        // Act
        await cut.Find("#trip-note-cancel").ClickAsync();

        // Assert
        var result = await dialog.Result;
        result!.Canceled.Should().BeTrue();
        await writer.DidNotReceive().UpdateAsync(
            Arg.Any<TripNoteModel>(),
            Arg.Any<TripStorageEnum>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnEditedTimeBeforeTheTripStarted()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var writer = WriterThatUpdates();
        await using var context = CreateContext(writer);
        var (cut, dialog) = await ShowModalAsync(
            context,
            endedOn: EndedOn,
            existingNote: ExistingNote());
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("09:00"));

        // Act
        cut.Find("#trip-note-time").Input("06:30");

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-recorded-on-invalid").TextContent
                .Should().Contain("before the trip started"));
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        dialog.Result.IsCompleted.Should().BeFalse();
        await writer.DidNotReceive().UpdateAsync(
            Arg.Any<TripNoteModel>(),
            Arg.Any<TripStorageEnum>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnEditedTimeAfterTheTripFinished()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var writer = WriterThatUpdates();
        await using var context = CreateContext(writer);
        var (cut, _) = await ShowModalAsync(
            context,
            endedOn: EndedOn,
            existingNote: ExistingNote());
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("09:00"));

        // Act
        cut.Find("#trip-note-time").Input("17:30");

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-recorded-on-invalid").TextContent
                .Should().Contain("after the trip finished"));
        await writer.DidNotReceive().UpdateAsync(
            Arg.Any<TripNoteModel>(),
            Arg.Any<TripStorageEnum>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheChangedTextAndTimeOfALocalNote()
    {
        // Arrange
        var writer = WriterThatUpdates();
        await using var context = CreateContext(writer);
        var (cut, dialog) = await ShowModalAsync(
            context,
            endedOn: EndedOn,
            existingNote: ExistingNote());
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("09:00"));

        // Act
        cut.Find("#trip-note-time").Input("11:45");
        cut.Find("#trip-note-text").Input("  changed to olive nymph  ");
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        await writer.Received(1).UpdateAsync(
            Arg.Is<TripNoteModel>(note =>
                note.Id == NoteId
                && note.TripId == TripId
                && note.Text == "changed to olive nymph"
                && note.RecordedOn == DateTimeOffset.Parse("2026-08-17T11:45:00Z")),
            TripStorageEnum.LocalFirst,
            Arg.Any<CancellationToken>());
        var result = await dialog.Result;
        result!.Canceled.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldSendAHistoricalNoteEditThroughTheServer()
    {
        // Arrange
        var writer = WriterThatUpdates();
        await using var context = CreateContext(writer);
        var (cut, _) = await ShowModalAsync(
            context,
            endedOn: EndedOn,
            storage: TripStorageEnum.Server,
            existingNote: ExistingNote());
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("09:00"));

        // Act
        cut.Find("#trip-note-text").Input("fish started rising beside the reeds");
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        await writer.Received(1).UpdateAsync(
            Arg.Is<TripNoteModel>(note => note.Text == "fish started rising beside the reeds"),
            TripStorageEnum.Server,
            Arg.Any<CancellationToken>());
        await writer.DidNotReceive().AddAsync(
            Arg.Any<TripNoteDraftModel>(),
            Arg.Any<TripStorageEnum>(),
            Arg.Any<CancellationToken>());
    }
}
