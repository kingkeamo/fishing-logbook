using System.Globalization;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.ActiveTripBannerViewModelTests;

public abstract class BaseActiveTripBannerViewModelTest
{
    protected static readonly Guid ActiveId = Guid.Parse("a1111111-1111-1111-1111-111111111111");

    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ITripService MockTrips = Substitute.For<ITripService>();
    protected readonly IAnglerPreferencesService MockPreferences = Substitute.For<IAnglerPreferencesService>();
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IRouteNavigator MockNavigator = Substitute.For<IRouteNavigator>();
    protected readonly MutableTimeProvider Clock = new(Now);
    protected readonly ActiveTripBannerViewModel Sut;

    protected BaseActiveTripBannerViewModelTest()
    {
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns((AnglerPreferencesModel?)null);
        Sut = new ActiveTripBannerViewModel(MockTrips, MockPreferences, MockSession, new NavigationService(MockNavigator), Text, Clock);
    }

    protected static TripSummaryDto Summary(Guid id, string status = TripConstants.Active, string role = TripParticipantConstants.Owner)
    {
        return new TripSummaryDto(id, status, Now.AddHours(-2), null) { Role = role, OwnerUserId = ViewerId };
    }

    protected void ServeTrips(params TripSummaryDto[] summaries)
    {
        MockTrips.LoadAsync(Arg.Any<CancellationToken>()).Returns(new TripHistoryModel(summaries, new HashSet<Guid>(), false));
    }
}
