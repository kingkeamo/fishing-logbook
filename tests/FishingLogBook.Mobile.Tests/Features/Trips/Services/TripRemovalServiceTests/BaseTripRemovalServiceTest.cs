using System.Globalization;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripRemovalServiceTests;

public abstract class BaseTripRemovalServiceTest
{
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ITripService MockTrips = Substitute.For<ITripService>();
    protected readonly ITripCatchService MockTripCatches = Substitute.For<ITripCatchService>();
    protected readonly IInteractionPresenter MockPresenter = Substitute.For<IInteractionPresenter>();
    protected readonly TripRemovalService Sut;

    protected BaseTripRemovalServiceTest()
    {
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        Sut = new TripRemovalService(MockTrips, MockTripCatches, new InteractionService(MockPresenter, Text), Text);
    }

    protected void AnswerConfirmation(bool confirmed)
    {
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(confirmed);
    }
}
