using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Components.TripSelector;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripSelectorTests;

public class WhenTestingSelection : BaseTripSelectorTest
{
    [Fact]
    public async Task ItShouldRenderTripsAsPillsAndPublishTheSelectedIdentity()
    {
        // Arrange
        var first = Trip("Morning session", "11111111-1111-1111-1111-111111111111");
        var second = Trip(null, "22222222-2222-2222-2222-222222222222");
        Guid? selected = null;
        await using var context = CreateContext();
        var cut = context.Render<TripSelector>(parameters => parameters
            .Add(component => component.Trips, [first, second])
            .Add(component => component.SelectedTripId, first.Id)
            .Add(component => component.SelectedTripIdChanged, tripId => selected = tripId)
            .Add(component => component.Label, "Add to existing Trip"));

        // Act
        cut.Find($"#trip-selector-{second.Id:D}").Click();

        // Assert
        cut.Find("#trip-selector").TextContent.Should().Contain("Add to existing Trip");
        cut.Find($"#trip-selector-{first.Id:D}").ClassList.Should().Contain("mud-chip-filled");
        cut.Find($"#trip-selector-{second.Id:D}").TextContent.Should().Contain("2026");
        selected.Should().Be(second.Id);
    }

    private static TripSummaryDto Trip(string? title, string id)
    {
        return new TripSummaryDto(
            Guid.Parse(id),
            TripConstants.Completed,
            DateTimeOffset.Parse("2026-08-27T14:42:00Z"),
            DateTimeOffset.Parse("2026-08-27T14:46:00Z"))
        {
            Title = title
        };
    }
}
