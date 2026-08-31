using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Layouts.OfflineLayout;
using Microsoft.AspNetCore.Components;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Layouts.OfflineLayoutTests;

public class WhenTestingActiveTrip : BaseOfflineLayoutTest
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    [Fact]
    public async Task ItShouldKeepTheActiveTripOutOfOfflineNavigationWhenNoneIsStored()
    {
        // Arrange
        var activeTrip = NoActiveTrip();
        await using var context = CreateContext(out _, activeTrip: activeTrip);

        // Act
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));
        await Task.Yield();

        // Assert
        cut.FindAll("#offline-trips-nav-link").Should().BeEmpty();
        cut.FindAll("#active-trip-banner").Should().BeEmpty();
        await activeTrip.Received(2).GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExposeTheLocalActiveTripInOfflineNavigationAndTheBanner()
    {
        // Arrange
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new TripModel(TripId, OwnerUserId, TripConstants.Active, StartedOn));
        var localOwner = Substitute.For<ILocalCatchOwnerService>();
        await using var context = CreateContext(out _, activeTrip: activeTrip, localOwner: localOwner);

        // Act
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#offline-trips-nav-link").GetAttribute("href")
                .Should().Be($"/offline/trips/{TripId:D}");
            cut.Find("#active-trip-banner").Should().NotBeNull();
            cut.Find("#active-trip-banner-view").GetAttribute("href")
                .Should().Be($"/offline/trips/{TripId:D}");
            cut.Find("#active-trip-banner-update").GetAttribute("href")
                .Should().Be($"/offline/trips/{TripId:D}/edit");
        });
        await activeTrip.Received(2).GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await localOwner.DidNotReceive().GetUserIdAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldHideOfflineTripEditWhenTheStoredTripIsNotOwnedByTheViewer()
    {
        // Arrange
        var otherOwner = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var activeTrip = Substitute.For<IActiveTripService>();
        activeTrip.GetActiveAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(new TripModel(
                TripId,
                otherOwner,
                TripConstants.Active,
                StartedOn,
                ParticipantUserIds: [OwnerUserId],
                Origin: TripOriginEnum.Server));
        await using var context = CreateContext(out _, activeTrip: activeTrip);

        // Act
        var cut = context.Render<OfflineLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(_ => { })));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#offline-trips-nav-link").GetAttribute("href")
                .Should().Be($"/offline/trips/{TripId:D}");
            cut.Find("#active-trip-banner-view").GetAttribute("href")
                .Should().Be($"/offline/trips/{TripId:D}");
            cut.FindAll("#active-trip-banner-update").Should().BeEmpty();
        });
    }
}
