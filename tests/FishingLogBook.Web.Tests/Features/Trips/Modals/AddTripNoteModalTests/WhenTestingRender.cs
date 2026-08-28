using AngleSharp.Html.Dom;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripNoteStoreTests;
using FishingLogBook.Web.Tests.TestSupport;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripNoteModalTests;

public class WhenTestingRender : BaseAddTripNoteModalTest
{
    [Fact]
    public async Task ItShouldNotOfferToSaveAnEmptyNote()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);

        // Act
        var (cut, dialog) = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue());
        cut.Find("#trip-note-modal-title").TextContent.Should().Contain("Add note");
        cut.Find("#trip-note-recorded-on").GetAttribute("type").Should().Be("datetime-local");
        store.Count.Should().Be(0);
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldOpenOnTheTripDateWithTheCurrentTime()
    {
        // Arrange
        var offset = OffsetPuttingLocalTimeAt(new TimeSpan(23, 59, 0));
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store, time: TestTimeService.WithOffset(offset));

        // Act
        var (cut, _) = await ShowModalAsync(context);

        // Assert
        var tripLocal = TestTimeService.ToDateTimeLocal(StartedOn, offset);
        cut.WaitForAssertion(() =>
            RecordedOnValue(cut).Should().Be($"{tripLocal[..11]}23:59"));
    }

    [Fact]
    public async Task ItShouldNotOpenBeforeTheTripStarted()
    {
        // Arrange
        var offset = OffsetPuttingLocalTimeAt(TimeSpan.Zero);
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store, time: TestTimeService.WithOffset(offset));

        // Act
        var (cut, _) = await ShowModalAsync(context);

        // Assert
        var tripLocal = TestTimeService.ToDateTimeLocal(StartedOn, offset);
        cut.WaitForAssertion(() => RecordedOnValue(cut).Should().Be(tripLocal));
    }

    [Fact]
    public async Task ItShouldStayOnTheTripDateForATripThatStartedOnAnEarlierDay()
    {
        // Arrange
        var offset = OffsetPuttingLocalTimeAt(new TimeSpan(23, 59, 0));
        var startedOn = StartedOn.AddDays(-3);
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store, time: TestTimeService.WithOffset(offset));

        // Act
        var (cut, _) = await ShowModalAsync(context, startedOn);

        // Assert
        var tripLocal = TestTimeService.ToDateTimeLocal(startedOn, offset);
        cut.WaitForAssertion(() =>
            RecordedOnValue(cut).Should().Be($"{tripLocal[..11]}23:59"));
    }

    [Fact]
    public async Task ItShouldShowFrenchNoteCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = new MemoryTripNoteStore();
        await using var context = CreateContext(store);

        // Act
        var (cut, _) = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-modal").TextContent.Should().Contain("Date et heure de la note"));
        cut.Find("#trip-note-modal-title").TextContent.Should().Contain("Ajouter une note");
        cut.Find("#trip-note-save").TextContent.Should().Contain("Ajouter une note");
        cut.Find("#trip-note-modal").TextContent.Should()
            .Contain("Modifiez l'heure pour placer cette note sur la chronologie de la sortie.");
    }

    private static string RecordedOnValue(IRenderedComponent<MudBlazor.MudDialogProvider> cut)
    {
        return ((IHtmlInputElement)cut.Find("#trip-note-recorded-on")).Value;
    }
}
