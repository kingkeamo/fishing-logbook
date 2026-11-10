using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;
using OfflineActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.OfflineActiveTrip.OfflineActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingElapsedLabel : BaseActiveTripTest
{
    [Fact]
    public async Task ItShouldShowOnlyMinutesForATripUnderAnHourOld()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<Web.Features.Trips.Offline.Stores.ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(StoredActiveTrip(startedOn: DateTimeOffset.UtcNow.AddMinutes(-20)));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-elapsed").Should().NotBeNull());
        var label = cut.Find("#active-trip-elapsed").TextContent;
        label.Should().MatchRegex(@"\b\d{1,2}m\b").And.NotContain("h");
        await store.Received().GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowOnlyMinutesForAnOfflineTripUnderAnHourOld()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<Web.Features.Trips.Offline.Stores.ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(StoredActiveTrip(startedOn: DateTimeOffset.UtcNow.AddMinutes(-20)));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<OfflineActiveTripPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#active-trip-elapsed").Should().NotBeNull());
        var label = cut.Find("#active-trip-elapsed").TextContent;
        label.Should().MatchRegex(@"\b\d{1,2}m\b").And.NotContain("h");
        await store.Received().GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>());
    }
}
