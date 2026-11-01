using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.PlannedTripServiceTests;

public sealed class WhenTestingGetEligible : BasePlannedTripServiceTest
{
    private static readonly Guid ServerTripId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task ItShouldRejectWithoutASignedInAccount()
    {
        // Arrange
        MockSessionCoordinator.Capture().Returns((FishingLogBook.Mobile.Features.Authentication.Models.AccountSessionLeaseModel?)null);

        // Act
        var act = () => Sut.GetEligibleAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldNotCallTheServerWhenOfflineAndStillOfferLocalPlans()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var plan = await Sut.PlanAsync("Corrib", null, Now.AddHours(5), CancellationToken.None);

        // Act
        var result = await Sut.GetEligibleAsync(CancellationToken.None);

        // Assert
        result.Should().ContainSingle(option => option.TripId == plan.Detail.Trip.Id && option.IsOrganiser);
        await MockClient.DidNotReceive().ListPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFallBackToLocalPlansWhenTheServerReadFails()
    {
        // Arrange
        FailPlannedRead();
        var plan = await Sut.PlanAsync("Corrib", null, Now, CancellationToken.None);

        // Act
        var result = await Sut.GetEligibleAsync(CancellationToken.None);

        // Assert
        result.Should().ContainSingle(option => option.TripId == plan.Detail.Trip.Id);
    }

    [Theory]
    [InlineData(-72, true)]
    [InlineData(72, true)]
    [InlineData(-73, false)]
    [InlineData(73, false)]
    public async Task ItShouldOfferOnlyPlansWithinThreeDaysInclusive(int hours, bool expected)
    {
        // Arrange
        await Sut.PlanAsync("Local", null, Now.AddHours(hours), CancellationToken.None);
        ServePlanned(ServerOption(ServerTripId, Now.AddHours(hours)));

        // Act
        var result = await Sut.GetEligibleAsync(CancellationToken.None);

        // Assert
        result.Count.Should().Be(expected ? 2 : 0);
    }

    [Fact]
    public async Task ItShouldNotOfferCancelledOrActivePlans()
    {
        // Arrange
        var cancelled = await Sut.PlanAsync("Cancelled", null, Now, CancellationToken.None);
        await Sut.CancelPlanAsync(cancelled, CancellationToken.None);
        var activated = await Sut.PlanAsync("Activated", null, Now, CancellationToken.None);
        await Sut.ActivateAsync(
            (await Sut.GetEligibleAsync(CancellationToken.None)).Single(option => option.TripId == activated.Detail.Trip.Id),
            CancellationToken.None);

        // Act
        var result = await Sut.GetEligibleAsync(CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldMergeServerAndLocalPlansOnceInPlannedOrder()
    {
        // Arrange
        var later = await Sut.PlanAsync("Later", null, Now.AddHours(30), CancellationToken.None);
        ServePlanned(
            ServerOption(later.Detail.Trip.Id, Now.AddHours(30), organiser: true),
            ServerOption(ServerTripId, Now.AddHours(2)));

        // Act
        var result = await Sut.GetEligibleAsync(CancellationToken.None);

        // Assert
        result.Select(option => option.TripId).Should().Equal(ServerTripId, later.Detail.Trip.Id);
        var shared = result.Single(option => option.TripId == ServerTripId);
        shared.IsOrganiser.Should().BeFalse();
        shared.OrganiserName.Should().Be("Sam");
        shared.Guests.Should().HaveCount(2);
        shared.Guests.Single(guest => guest.DisplayName == "Pat").IsPending.Should().BeTrue();
        await MockClient.Received(1).ListPlannedAsync(null, Arg.Any<CancellationToken>());
    }
}
