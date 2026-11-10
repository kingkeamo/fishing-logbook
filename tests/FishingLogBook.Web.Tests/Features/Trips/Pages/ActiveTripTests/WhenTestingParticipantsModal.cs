using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Modals.TripParticipants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingParticipantsModal : BaseActiveTripTest
{
    private static ITripStore EmptyStore()
    {
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        return store;
    }

    private static ITripClient ClientWithActiveTrip()
    {
        var detail = new TripDetailDto(new TripViewDto(TripId, OwnerUserId, TripConstants.Active, StartedOn, null))
        {
            Role = TripParticipantConstants.Owner
        };
        var client = Substitute.For<ITripClient>();
        client.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(detail);
        return client;
    }

    [Fact]
    public async Task ItShouldNotReloadTheTripWhenTheParticipantsModalIsDismissed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var modalService = ConfirmingModalService();
        var tripClient = ClientWithActiveTrip();
        await using var context = CreateContext(EmptyStore(), modalService: modalService, tripClient: tripClient);
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(page => page.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#active-trip-participants").Should().NotBeNull());

        // Act
        await cut.Find("#active-trip-participants").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<TripParticipantsModal, TripParticipantsModalModel, TripParticipantsModalResult>(
                Arg.Is<TripParticipantsModalModel>(model => model.TripId == TripId),
                Arg.Any<CancellationToken>());
        await tripClient.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadTheTripWhenTheParticipantsWereChanged()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var modalService = ConfirmingModalService();
        modalService.ShowAsync<TripParticipantsModal, TripParticipantsModalModel, TripParticipantsModalResult>(
                Arg.Any<TripParticipantsModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new TripParticipantsModalResult(new TripParticipantsDto(TripId, TripParticipantConstants.Owner)));
        var tripClient = ClientWithActiveTrip();
        await using var context = CreateContext(EmptyStore(), modalService: modalService, tripClient: tripClient);
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(page => page.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#active-trip-participants").Should().NotBeNull());

        // Act
        await cut.Find("#active-trip-participants").ClickAsync();

        // Assert
        await tripClient.Received(2).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
        cut.Find("#active-trip-participants").Should().NotBeNull();
    }
}
