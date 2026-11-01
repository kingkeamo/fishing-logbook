using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using OfflineTripEditPage = FishingLogBook.Web.Features.Trips.Pages.OfflineTripEdit.OfflineTripEdit;
using TripEditPage = FishingLogBook.Web.Features.Trips.Pages.TripEdit.TripEdit;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripEditTests;

public class WhenTestingPlannedRedirect : BaseTripEditTest
{
    [Theory]
    [InlineData(TripConstants.Planned)]
    [InlineData(TripConstants.Cancelled)]
    public async Task ItShouldSendAPlannedOrCancelledTripToThePlanEditor(string status)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip() with { Status = status, PlannedStartOn = StartedOn };
        await using var context = CreateContext(StoreWithTrip(trip));

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith($"/trips/{TripId:D}/plan"));
    }

    [Theory]
    [InlineData(TripConstants.Planned)]
    [InlineData(TripConstants.Cancelled)]
    public async Task ItShouldSendAPlannedOrCancelledTripToTheOfflinePlanEditor(string status)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = ActiveTrip() with { Status = status, PlannedStartOn = StartedOn };
        await using var context = CreateContext(StoreWithTrip(trip));

        // Act
        var cut = context.Render<OfflineTripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            context.Services.GetRequiredService<NavigationManager>().Uri
                .Should().EndWith($"/offline/trips/{TripId:D}/plan"));
    }
}
