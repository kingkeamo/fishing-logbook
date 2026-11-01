using System.Globalization;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripStartServiceTests;

public abstract class BaseTripStartServiceTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid PlannedTripId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    protected static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ITripService MockTrips = Substitute.For<ITripService>();
    protected readonly IPlannedTripService MockPlanned = Substitute.For<IPlannedTripService>();
    protected readonly IPlannedTripChooser MockChooser = Substitute.For<IPlannedTripChooser>();
    protected readonly IAnglerPreferencesService MockPreferences = Substitute.For<IAnglerPreferencesService>();
    protected readonly IInteractionPresenter MockPresenter = Substitute.For<IInteractionPresenter>();
    protected readonly TripStartService Sut;

    protected BaseTripStartServiceTest()
    {
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns(PlannedTripLookupModel.Succeeded([]));
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns((AnglerPreferencesModel?)null);
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        MockTrips.StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => Started(Guid.NewGuid()));
        Sut = new TripStartService(MockTrips, MockPlanned, MockChooser, MockPreferences, new InteractionService(MockPresenter, Text), Text);
    }

    protected static PlannedTripOptionModel Option()
    {
        return new PlannedTripOptionModel(PlannedTripId, Now, OwnerUserId, "Alex", IsOrganiser: true) { Title = "Corrib" };
    }

    protected static PlannedTripLookupModel Found(params PlannedTripOptionModel[] options)
    {
        return PlannedTripLookupModel.Succeeded(options);
    }

    protected static LocalTripRecordModel Started(Guid id)
    {
        return new LocalTripRecordModel
        {
            UserId = OwnerUserId,
            Detail = new TripDetailDto(new TripViewDto(id, OwnerUserId, TripConstants.Active, Now)) { Role = TripParticipantConstants.Owner }
        };
    }
}
