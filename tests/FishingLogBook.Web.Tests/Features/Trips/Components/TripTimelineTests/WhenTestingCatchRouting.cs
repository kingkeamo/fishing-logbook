using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Trips.Components.TripTimeline;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripTimelineTests;

public class WhenTestingCatchRouting : BaseTripTimelineTest
{
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ItShouldRouteTheViewersOwnCatchToEdit()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                [
                    Item(
                        TripTimelineKindEnum.Catch,
                        StartedOn.AddMinutes(30),
                        "Pike",
                        catchId: CatchId,
                        contributedByUserId: OwnerUserId,
                        recordedByUserId: OwnerUserId)
                ])
            .Add(component => component.ViewerUserId, OwnerUserId));

        // Assert
        cut.Find($"#trip-timeline-catch-{CatchId:D}-link").GetAttribute("href")
            .Should().Be($"/catches/{CatchId:D}/edit");
    }

    [Fact]
    public async Task ItShouldRouteACatchRecordedByTheViewerToEdit()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                [
                    Item(
                        TripTimelineKindEnum.Catch,
                        StartedOn.AddMinutes(30),
                        "Pike",
                        catchId: CatchId,
                        contributedByUserId: OtherUserId,
                        recordedByUserId: OwnerUserId)
                ])
            .Add(component => component.ViewerUserId, OwnerUserId));

        // Assert
        cut.Find($"#trip-timeline-catch-{CatchId:D}-link").GetAttribute("href")
            .Should().Be($"/catches/{CatchId:D}/edit");
    }

    [Fact]
    public async Task ItShouldRouteAnotherParticipantsCatchToTheReadOnlyView()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<TripTimeline>(parameters => parameters
            .Add(
                component => component.Items,
                [
                    Item(
                        TripTimelineKindEnum.Catch,
                        StartedOn.AddMinutes(30),
                        "Pike",
                        catchId: CatchId,
                        contributedByUserId: OtherUserId,
                        recordedByUserId: OtherUserId)
                ])
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.CatchBaseHref, "/offline/catches"));

        // Assert
        cut.Find($"#trip-timeline-catch-{CatchId:D}-link").GetAttribute("href")
            .Should().Be($"/catches/{CatchId:D}");
    }
}
