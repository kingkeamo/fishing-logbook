using System.Globalization;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Measurements;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripCatchPickerViewModelTests;

public abstract class BaseTripCatchPickerViewModelTest
{
    protected static readonly Guid One = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    protected static readonly Guid Two = Guid.Parse("a0000000-0000-0000-0000-000000000002");

    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ITripCatchService MockTripCatches = Substitute.For<ITripCatchService>();
    protected readonly IAccountUnitsService MockUnits = Substitute.For<IAccountUnitsService>();
    protected readonly TripCatchPickerViewModel Sut;

    protected BaseTripCatchPickerViewModelTest()
    {
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        Sut = new TripCatchPickerViewModel(MockTripCatches, MockUnits, new MeasurementService(), Text, new FixedTimeProvider(Now));
    }

    protected static TripCatchCandidateModel Candidate(Guid id, string? species = "Pike", decimal? weight = 2.5m)
    {
        return new TripCatchCandidateModel(id, Now.AddHours(-1), species, weight, null);
    }

    protected void ServeCandidates(params TripCatchCandidateModel[] candidates)
    {
        MockTripCatches.GetEligibleAsync(Arg.Any<FishingLogBook.Mobile.Features.Trips.Offline.Models.LocalTripRecordModel>(), Arg.Any<CancellationToken>())
            .Returns(candidates);
    }
}
