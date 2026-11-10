using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Components.TripTimeline;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripTimelineTests;

public class WhenTestingUnitsAndMedia : BaseTripTimelineTest
{
    [Fact]
    public async Task ItShouldShowMeasurementsInTheAnglersImperialUnits()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[]
                {
                    Item(
                        TripTimelineKindEnum.Catch,
                        StartedOn.AddMinutes(30),
                        "Pike",
                        catchId: CatchId,
                        weight: 2.5m,
                        length: 50m)
                })
            .Add(component => component.WeightUnit, WeightUnitEnum.Lb)
            .Add(component => component.LengthUnit, LengthUnitEnum.In));

        // Assert
        var label = cut.Find($"#trip-timeline-catch-{CatchId:D}-measurements").TextContent;
        label.Should().Contain("lb").And.Contain("in").And.NotContain("kg").And.NotContain("cm");
    }

    [Fact]
    public async Task ItShouldShowOnlyTheWeightWhenNoLengthWasRecorded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                new[] { Item(TripTimelineKindEnum.Catch, StartedOn.AddMinutes(30), "Pike", catchId: CatchId, weight: 1.5m) }));

        // Assert
        var label = cut.Find($"#trip-timeline-catch-{CatchId:D}-measurements").TextContent;
        label.Should().Contain("kg").And.NotContain("·");
    }

    [Fact]
    public async Task ItShouldRenderNoPhotographWhenTheStoredBytesAreEmpty()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithPhotographBytes();
        await using var context = CreateContext(tripPhotographStore: store);

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId)
            .Add(
                component => component.Items,
                new[] { Item(TripTimelineKindEnum.Photograph, StartedOn.AddMinutes(20), photographId: PhotographId) }));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("img").Should().BeEmpty());
        await store.Received(1).GetBytesAsync(OwnerUserId, TripId, PhotographId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogAndRenderNoPhotographWhenTheDeviceStoreFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITripPhotographStore>();
        store.GetBytesAsync(OwnerUserId, TripId, PhotographId, Arg.Any<CancellationToken>())
            .Returns<Task<byte[]?>>(_ => throw new InvalidOperationException("idb"));
        var logging = QuietLogging();
        await using var context = CreateContext(logging: logging, tripPhotographStore: store);

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.TripId, TripId)
            .Add(
                component => component.Items,
                new[] { Item(TripTimelineKindEnum.Photograph, StartedOn.AddMinutes(20), photographId: PhotographId) }));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("img").Should().BeEmpty());
        await logging.Received(1).LogErrorAsync(
            "reading a trip timeline photograph",
            Arg.Is<Exception>(exception => exception.Message == "idb"),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldFallBackToTheInstantWhenTheLocalValueCannotBeParsed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns("garbage");
        await using var context = CreateContext(time);

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.Items, new[] { Item(TripTimelineKindEnum.Started, StartedOn) }));

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#trip-timeline-started-{StartedOn.ToUnixTimeMilliseconds()}").TextContent
            .Should().Contain("06:00"));
        await time.Received(1).ToDateTimeLocalValueAsync(StartedOn, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLinkAnotherAnglersCatchToItsReadOnlyView()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var someoneElse = Guid.Parse("33333333-3333-3333-3333-333333333333");
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(
                component => component.Items,
                new[]
                {
                    Item(
                        TripTimelineKindEnum.Catch,
                        StartedOn.AddMinutes(30),
                        "Pike",
                        catchId: CatchId,
                        contributedByUserId: someoneElse,
                        recordedByUserId: someoneElse)
                }));

        // Assert
        cut.Find($"#trip-timeline-catch-{CatchId:D}-link").GetAttribute("href")
            .Should().Be($"/catches/{CatchId:D}");
    }
}
