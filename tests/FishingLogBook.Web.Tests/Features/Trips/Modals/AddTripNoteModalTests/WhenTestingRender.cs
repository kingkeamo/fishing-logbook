using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripNoteModalTests;

public class WhenTestingRender : BaseAddTripNoteModalTest
{
    [Fact]
    public async Task ItShouldNotOfferToSaveAnEmptyNote()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (cut, dialog) = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue());
        cut.Find("#trip-note-modal-title").TextContent.Should().Contain("Add note");
        cut.Find("#trip-note-date").GetAttribute("type").Should().Be("date");
        cut.Find("#trip-note-time").GetAttribute("type").Should().Be("time");
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldOpenOnTheTripDateWithTheCurrentTime()
    {
        // Arrange
        var offset = OffsetPuttingLocalTimeAt(new TimeSpan(23, 59, 0));
        await using var context = CreateContext(time: TestTimeService.WithOffset(offset));

        // Act
        var (cut, _) = await ShowModalAsync(context);

        // Assert
        var tripLocal = TestTimeService.ToDateTimeLocal(StartedOn, offset);
        cut.WaitForAssertion(() => DateValue(cut).Should().Be(tripLocal[..10]));
        TimeValue(cut).Should().Be("23:59");
    }

    [Fact]
    public async Task ItShouldOpenAtTheTripStartWhenTheCurrentTimeWouldFallBeforeIt()
    {
        // Arrange
        var offset = OffsetPuttingLocalTimeAt(TimeSpan.Zero);
        await using var context = CreateContext(time: TestTimeService.WithOffset(offset));

        // Act
        var (cut, _) = await ShowModalAsync(context);

        // Assert
        var tripLocal = TestTimeService.ToDateTimeLocal(StartedOn, offset);
        cut.WaitForAssertion(() => DateValue(cut).Should().Be(tripLocal[..10]));
        TimeValue(cut).Should().Be(tripLocal[11..16]);
    }

    [Fact]
    public async Task ItShouldOpenAtTheEndOfACompletedTripRatherThanNow()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn);

        // Assert
        cut.WaitForAssertion(() => DateValue(cut).Should().Be("2026-08-17"));
        TimeValue(cut).Should().Be("16:00");
        cut.FindAll("#trip-note-recorded-on-invalid").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepTheDateInsideTheTrip()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-date").GetAttribute("min").Should().Be("2026-08-17"));
        cut.Find("#trip-note-date").GetAttribute("max").Should().Be("2026-08-17");
    }

    [Fact]
    public async Task ItShouldShowFrenchNoteCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-modal").TextContent.Should().Contain("Heure"));
        cut.Find("#trip-note-modal-title").TextContent.Should().Contain("Ajouter une note");
        cut.Find("#trip-note-save").TextContent.Should().Contain("Ajouter une note");
        cut.Find("#trip-note-modal").TextContent.Should()
            .Contain("Modifiez l'heure pour placer cette note sur la chronologie de la sortie.");
    }

    [Fact]
    public async Task ItShouldShowTheFrenchCopyWhenTheTimeFallsOutsideTheTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn);
        cut.WaitForAssertion(() => TimeValue(cut).Should().Be("16:00"));

        // Act
        cut.Find("#trip-note-time").Input("06:00");

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-note-recorded-on-invalid").TextContent
                .Should().Contain("C'est avant le début de la sortie"));
        cut.Find("#trip-note-save").HasAttribute("disabled").Should().BeTrue();
    }
}
