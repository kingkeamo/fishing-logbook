using System.Globalization;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Measurements;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripEditViewModelTests;

public abstract class BaseTripEditViewModelTest
{
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ITripService MockTrips = Substitute.For<ITripService>();
    protected readonly ITripPhotographService MockPhotographs = Substitute.For<ITripPhotographService>();
    protected readonly ICatchStore MockCatchStore = Substitute.For<ICatchStore>();
    protected readonly IPhotographFileStore MockFiles = Substitute.For<IPhotographFileStore>();
    protected readonly IAnglerPreferencesService MockPreferences = Substitute.For<IAnglerPreferencesService>();
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IAccountUnitsService MockUnits = Substitute.For<IAccountUnitsService>();
    protected readonly TripEditViewModel Sut;

    protected BaseTripEditViewModelTest()
    {
        MockSession.Capture().Returns(new AccountSessionLeaseModel(ViewerId, 1, CancellationToken.None));
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns((AnglerPreferencesModel?)null);
        Sut = new TripEditViewModel(
            MockTrips, new TripTimelineService(), MockPhotographs, MockCatchStore, MockFiles, MockPreferences, MockSession, MockUnits,
            new MeasurementService(), Text, new FixedTimeProvider(Now));
    }

    protected async Task LoadAsync(LocalTripRecordModel trip)
    {
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(trip);
        await Sut.LoadAsync(TripId, CancellationToken.None);
    }

    protected void ServeSavedLocations(params string[] names)
    {
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(AnglerPreferencesModel.Empty with
        {
            Locations = new FishingLocationPreferencesDto([.. names.Select((name, index) => new FishingLocationPreferenceDto(Guid.NewGuid(), name, index == 0))])
        });
    }

    protected static TripCatchSummaryDto Catch(Guid id, Guid by, Guid? recordedBy = null)
    {
        return new TripCatchSummaryDto(id, Now.AddHours(-1)) { CaughtByUserId = by, RecordedByUserId = recordedBy ?? by, SpeciesName = "Pike", Weight = 2.5m };
    }
}
