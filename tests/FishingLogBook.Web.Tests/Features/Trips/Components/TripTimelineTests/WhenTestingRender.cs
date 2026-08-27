using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Trips.Components.TripTimeline;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripTimelineTests;

public class WhenTestingRender : BaseTripTimelineTest
{
    [Fact]
    public async Task ItShouldSayNothingHasHappenedYet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, Array.Empty<TripTimelineItemModel>()));

        // Assert
        cut.Find("#trip-timeline-empty").TextContent.Should()
            .Contain("Nothing has happened on this trip yet.");
    }

    [Fact]
    public async Task ItShouldStillRenderTheEntriesWhenTheLocalTimeCannotBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no interop"));
        var logging = QuietLogging();
        await using var context = CreateContext(time, logging);

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, new[] { Item(TripTimelineKindEnum.Started, StartedOn) }));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-timeline-started-{StartedOn.ToUnixTimeMilliseconds()}")
                .TextContent.Should().Contain("Fishing started"));
        await logging.Received(1).LogErrorAsync(
            "reading a trip timeline time",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLinkACatchEntryToTheCatch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[] { Item(TripTimelineKindEnum.Catch, StartedOn.AddMinutes(30), "Pike", catchId: CatchId) })
            .Add(component => component.CatchBaseHref, "/offline/catches"));

        // Assert
        var link = cut.Find($"#trip-timeline-catch-{CatchId:D}-link");
        link.GetAttribute("href").Should().Be($"/offline/catches?catchId={CatchId:D}");
        link.TextContent.Should().Contain("Pike");
    }

    [Fact]
    public async Task ItShouldDescribeACatchWithNoSpecies()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[] { Item(TripTimelineKindEnum.Catch, StartedOn.AddMinutes(30), catchId: CatchId) }));

        // Assert
        cut.Find($"#trip-timeline-catch-{CatchId:D}").TextContent.Should().Contain("Catch recorded");
    }

    [Fact]
    public async Task ItShouldRenderTheWholeTripInOrderWithLocalTimes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(TestTimeService.WithOffset(TimeSpan.FromHours(1)));
        var items = new[]
        {
            Item(TripTimelineKindEnum.Started, StartedOn),
            Item(TripTimelineKindEnum.Note, StartedOn.AddMinutes(15), text: "The wind dropped."),
            Item(TripTimelineKindEnum.Catch, StartedOn.AddMinutes(30), "Pike", catchId: CatchId),
            Item(TripTimelineKindEnum.Photograph, StartedOn.AddMinutes(45)),
            Item(TripTimelineKindEnum.Finished, StartedOn.AddHours(4))
        };

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, items));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#trip-timeline-started-{StartedOn.ToUnixTimeMilliseconds()}")
                .TextContent.Should().Contain("07:00"));
        cut.Find($"#trip-timeline-catch-{CatchId:D}").TextContent.Should().Contain("07:30");
        cut.Markup.Should().Contain("The wind dropped.");
        cut.Markup.Should().Contain("Trip photograph added");
        cut.Find($"#trip-timeline-finished-{StartedOn.AddHours(4).ToUnixTimeMilliseconds()}")
            .TextContent.Should().Contain("Fishing finished");
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, new[] { Item(TripTimelineKindEnum.Started, StartedOn) }));

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Chronologie de la sortie"));
        cut.Markup.Should().Contain("Pêche commencée");
    }
}
