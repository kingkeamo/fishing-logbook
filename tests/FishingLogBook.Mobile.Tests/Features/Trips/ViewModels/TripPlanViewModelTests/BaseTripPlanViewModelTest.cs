using System.Globalization;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripPlanViewModelTests;

public abstract class BaseTripPlanViewModelTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 40, 0, TimeSpan.Zero);

    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly IPlannedTripService MockPlanned = Substitute.For<IPlannedTripService>();
    protected readonly ITripService MockTrips = Substitute.For<ITripService>();
    protected readonly IAnglerPreferencesService MockPreferences = Substitute.For<IAnglerPreferencesService>();
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly IInteractionPresenter MockPresenter = Substitute.For<IInteractionPresenter>();
    protected readonly TripPlanViewModel Sut;

    protected BaseTripPlanViewModelTest(TimeZoneInfo? zone = null)
    {
        MockConnectivity.IsOnline.Returns(true);
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(AnglerPreferencesModel.Empty with
        {
            Locations = new FishingLocationPreferencesDto([new FishingLocationPreferenceDto(Guid.NewGuid(), "Lough Corrib", IsDefault: false)])
        });
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        Sut = new TripPlanViewModel(
            MockPlanned,
            MockTrips,
            MockPreferences,
            MockSession,
            MockConnectivity,
            new InteractionService(MockPresenter, Text),
            Text,
            new FixedTimeProvider(Now, zone));
    }

    protected static LocalTripRecordModel Plan(
        string status = TripConstants.Planned,
        Guid? owner = null,
        string role = TripParticipantConstants.Owner,
        long revision = 1,
        long syncedRevision = 1)
    {
        return new LocalTripRecordModel
        {
            UserId = OwnerUserId,
            Revision = revision,
            SyncedRevision = syncedRevision,
            Detail = new TripDetailDto(new TripViewDto(TripId, owner ?? OwnerUserId, status, Now.AddDays(1))
            {
                PlannedStartOn = Now.AddDays(1),
                Title = "Corrib",
                PlaceName = "Shore"
            })
            { Role = role }
        };
    }
}
