using AngleSharp.Html.Dom;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripNote;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripNoteModalTests;

public class WhenTestingSave : BaseAddTripNoteModalTest
{
    [Fact]
    public async Task ItShouldNotSaveAWhitespaceOnlyNote()
    {
        // Arrange
        var writer = WriterThatSaves();
        await using var context = CreateContext(writer);
        var (cut, dialog) = await ShowModalAsync(context);

        // Act
        cut.Find("#trip-note-text").Input("   \t  ");

        // Assert
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        await cut.Find("#trip-note-save").ClickAsync();
        await ShouldNotHaveWrittenAsync(writer);
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotSaveANoteOverTheCap()
    {
        // Arrange
        var writer = WriterThatSaves();
        await using var context = CreateContext(writer);
        var (cut, dialog) = await ShowModalAsync(context);

        // Act
        cut.Find("#trip-note-text").Input(new string('a', TripConstants.MaxNoteTextLength + 1));

        // Assert
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        await ShouldNotHaveWrittenAsync(writer);
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotSaveWhenTheNoteDateIsCleared()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var writer = WriterThatSaves();
        await using var context = CreateContext(writer);
        var (cut, dialog) = await ShowModalAsync(context);
        cut.Find("#trip-note-text").Input("wind picked up");

        // Act
        cut.Find("#trip-note-date").Input(string.Empty);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-recorded-on-invalid").TextContent
                .Should().Contain("Enter a valid date and time for this note."));
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        await ShouldNotHaveWrittenAsync(writer);
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRejectATimeBeforeTheTripStarted()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var offset = OffsetPuttingLocalTimeAt(new TimeSpan(12, 0, 0));
        var writer = WriterThatSaves();
        await using var context = CreateContext(writer, time: TestTimeService.WithOffset(offset));
        var startedOn = DateTimeOffset.UtcNow.AddHours(-2);
        var (cut, dialog) = await ShowModalAsync(context, startedOn);
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("12:00"));
        cut.Find("#trip-note-text").Input("fish rising near the reeds");

        // Act
        cut.Find("#trip-note-time").Input("09:00");

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-recorded-on-invalid").TextContent
                .Should().Contain("before the trip started"));
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        await ShouldNotHaveWrittenAsync(writer);
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRejectATimeInTheFutureOnAnActiveTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var offset = OffsetPuttingLocalTimeAt(new TimeSpan(12, 0, 0));
        var writer = WriterThatSaves();
        await using var context = CreateContext(writer, time: TestTimeService.WithOffset(offset));
        var startedOn = DateTimeOffset.UtcNow.AddHours(-2);
        var (cut, dialog) = await ShowModalAsync(context, startedOn);
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("12:00"));
        cut.Find("#trip-note-text").Input("fish rising near the reeds");

        // Act
        cut.Find("#trip-note-time").Input("13:30");

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-recorded-on-invalid").TextContent
                .Should().Contain("in the future"));
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        await ShouldNotHaveWrittenAsync(writer);
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRejectATimeAfterACompletedTripFinished()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var writer = WriterThatSaves();
        await using var context = CreateContext(writer);
        var (cut, dialog) = await ShowModalAsync(context, endedOn: EndedOn);
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("16:00"));
        cut.Find("#trip-note-text").Input("a good day, three brownies");

        // Act
        cut.Find("#trip-note-time").Input("18:30");

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-recorded-on-invalid").TextContent
                .Should().Contain("after the trip finished"));
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        await ShouldNotHaveWrittenAsync(writer);
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldKeepTheTypedNoteWhenTheLocalWriteFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(WriterThatFails(), logging);
        var (cut, dialog) = await ShowModalAsync(context);
        cut.Find("#trip-note-text").Input("fish rising near the reeds");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        cut.Find("#trip-note-add-failed").TextContent.Should().Contain("could not be added");
        ((IHtmlTextAreaElement)cut.Find("#trip-note-text")).Value
            .Should().Be("fish rising near the reeds");
        dialog.Result.IsCompleted.Should().BeFalse();
        await logging.Received(1).LogErrorAsync(
            "adding a trip note",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAskTheAnglerToGoOnlineWhenAHistoricalTripCannotBeReached()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(WriterThatCannotReachTheServer(), logging);
        var (cut, dialog) = await ShowModalAsync(
            context,
            endedOn: EndedOn,
            storage: TripNoteStorageEnum.Server);
        cut.Find("#trip-note-text").Input("fish started rising beside the reeds");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        cut.Find("#trip-note-add-failed").TextContent
            .Should().Contain("You need to be online to add a note to this trip.");
        ((IHtmlTextAreaElement)cut.Find("#trip-note-text")).Value
            .Should().Be("fish started rising beside the reeds");
        dialog.Result.IsCompleted.Should().BeFalse();
        await logging.Received(1).LogErrorAsync(
            "adding a trip note",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNeverLogTheNoteText()
    {
        // Arrange
        const string secret = "met Sarah about the lease at the bailiff hut";
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(WriterThatFails(), logging);
        var (cut, _) = await ShowModalAsync(context);
        cut.Find("#trip-note-text").Input(secret);

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Is<string>(operation => operation.Contains(secret)),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Is<Exception>(exception => exception.Message.Contains(secret)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCloseWithoutSavingWhenCancelled()
    {
        // Arrange
        var writer = WriterThatSaves();
        await using var context = CreateContext(writer);
        var (cut, dialog) = await ShowModalAsync(context);
        cut.Find("#trip-note-text").Input("changed to olive nymph");

        // Act
        await cut.Find("#trip-note-cancel").ClickAsync();

        // Assert
        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeTrue();
        await ShouldNotHaveWrittenAsync(writer);
    }

    [Fact]
    public async Task ItShouldSaveALocalNoteAtTheChosenTime()
    {
        // Arrange
        var writer = WriterThatSaves();
        await using var context = CreateContext(writer);
        var (cut, dialog) = await ShowModalAsync(context, endedOn: EndedOn);
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("16:00"));

        // Act
        cut.Find("#trip-note-time").Input("11:30");
        cut.Find("#trip-note-text").Input("  changed to olive nymph  ");
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        await writer.Received(1).AddAsync(
            Arg.Is<TripNoteDraftModel>(draft =>
                draft.TripId == TripId
                && draft.OwnerUserId == OwnerUserId
                && draft.Text == "changed to olive nymph"
                && draft.RecordedOn == DateTimeOffset.Parse("2026-08-17T11:30:00Z")),
            TripNoteStorageEnum.LocalFirst,
            Arg.Any<CancellationToken>());
        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeFalse();
        result.Data.Should().BeOfType<AddTripNoteModalResult>()
            .Which.Note.RecordedOn.Should().Be(DateTimeOffset.Parse("2026-08-17T11:30:00Z"));
    }

    [Fact]
    public async Task ItShouldSendAHistoricalNoteToTheServerAtTheChosenTime()
    {
        // Arrange
        var writer = WriterThatSaves();
        await using var context = CreateContext(writer);
        var (cut, dialog) = await ShowModalAsync(
            context,
            endedOn: EndedOn,
            storage: TripNoteStorageEnum.Server);
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("16:00"));

        // Act
        cut.Find("#trip-note-time").Input("11:30");
        cut.Find("#trip-note-text").Input("fish started rising beside the reeds");
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        await writer.Received(1).AddAsync(
            Arg.Is<TripNoteDraftModel>(draft =>
                draft.TripId == TripId
                && draft.Text == "fish started rising beside the reeds"
                && draft.RecordedOn == DateTimeOffset.Parse("2026-08-17T11:30:00Z")),
            TripNoteStorageEnum.Server,
            Arg.Any<CancellationToken>());
        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeFalse();
    }

    private static async Task ShouldNotHaveWrittenAsync(
        Web.Features.Trips.Services.ITripNoteWriteService writer)
    {
        await writer.DidNotReceive().AddAsync(
            Arg.Any<TripNoteDraftModel>(),
            Arg.Any<TripNoteStorageEnum>(),
            Arg.Any<CancellationToken>());
    }
}
