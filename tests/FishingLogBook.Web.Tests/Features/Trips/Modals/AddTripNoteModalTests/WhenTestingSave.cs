using AngleSharp.Html.Dom;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Modals.AddTripNote;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripNoteModalTests;

public class WhenTestingSave : BaseAddTripNoteModalTest
{
    [Fact]
    public async Task ItShouldNotSaveAWhitespaceOnlyNote()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var (cut, dialog) = await ShowModalAsync(context);

        // Act
        cut.Find("#trip-note-text").Input("   \t  ");

        // Assert
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        await cut.Find("#trip-note-save").ClickAsync();
        store.Count.Should().Be(0);
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotSaveANoteOverTheCap()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var (cut, dialog) = await ShowModalAsync(context);

        // Act
        cut.Find("#trip-note-text").Input(new string('a', TripConstants.MaxNoteTextLength + 1));

        // Assert
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
        store.Count.Should().Be(0);
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotSaveWhenTheNoteTimeIsCleared()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var (cut, dialog) = await ShowModalAsync(context);
        cut.Find("#trip-note-text").Input("wind picked up");

        // Act
        cut.Find("#trip-note-recorded-on").Input(string.Empty);
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        cut.Find("#trip-note-recorded-on-invalid").TextContent
            .Should().Contain("Enter a valid date and time for this note.");
        store.Count.Should().Be(0);
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldKeepTheTypedNoteWhenTheLocalWriteFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripNoteStore { FailWrite = true };
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(store, logging);
        var (cut, dialog) = await ShowModalAsync(context);
        cut.Find("#trip-note-text").Input("fish rising near the reeds");

        // Act
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        cut.Find("#trip-note-add-failed").TextContent.Should().Contain("could not be added");
        ((IHtmlTextAreaElement)cut.Find("#trip-note-text")).Value
            .Should().Be("fish rising near the reeds");
        store.Count.Should().Be(0);
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
        var store = new MemoryTripNoteStore { FailWrite = true };
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(store, logging);
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
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var (cut, dialog) = await ShowModalAsync(context);
        cut.Find("#trip-note-text").Input("changed to olive nymph");

        // Act
        await cut.Find("#trip-note-cancel").ClickAsync();

        // Assert
        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeTrue();
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldSaveTheNoteAtTheChosenTime()
    {
        // Arrange
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);
        var (cut, dialog) = await ShowModalAsync(context);

        // Act
        cut.Find("#trip-note-recorded-on").Input("2026-08-17T14:35");
        cut.Find("#trip-note-text").Input("  changed to olive nymph  ");
        await cut.Find("#trip-note-save").ClickAsync();

        // Assert
        store.Count.Should().Be(1);
        var stored = store.All().Single();
        stored.Text.Should().Be("changed to olive nymph");
        stored.RecordedOn.Should().Be(DateTimeOffset.Parse("2026-08-17T14:35:00Z"));
        stored.TripId.Should().Be(TripId);
        stored.OwnerUserId.Should().Be(OwnerUserId);
        stored.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeFalse();
        result.Data.Should().BeOfType<AddTripNoteModalResult>()
            .Which.Note.Should().Be(stored);
    }
}
