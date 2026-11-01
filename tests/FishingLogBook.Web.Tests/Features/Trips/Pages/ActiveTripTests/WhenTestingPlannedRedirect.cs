using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;
using OfflineActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.OfflineActiveTrip.OfflineActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingPlannedRedirect : BaseActiveTripTest
{
    private static readonly DateTimeOffset PlannedStart = DateTimeOffset.Parse("2026-09-12T06:30:00Z");

    private static ITripStore StoreWith(string status)
    {
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(new TripModel(
                TripId,
                OwnerUserId,
                status,
                PlannedStart,
                PlannedStartOn: PlannedStart));
        return store;
    }

    [Theory]
    [InlineData(TripConstants.Planned)]
    [InlineData(TripConstants.Cancelled)]
    public async Task ItShouldSendAPlannedOrCancelledTripToThePlanEditor(string status)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(StoreWith(status));

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

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
        await using var context = CreateContext(StoreWith(status));

        // Act
        var cut = context.Render<OfflineActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() =>
            context.Services.GetRequiredService<NavigationManager>().Uri
                .Should().EndWith($"/offline/trips/{TripId:D}/plan"));
    }
}
