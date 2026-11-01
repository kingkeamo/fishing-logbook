using System.Globalization;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripListViewModelTests;

public abstract class BaseTripListViewModelTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid FriendUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ITripService MockTrips = Substitute.For<ITripService>();
    protected readonly ITripParticipantClient MockParticipants = Substitute.For<ITripParticipantClient>();
    protected readonly IAnglerPreferencesService MockPreferences = Substitute.For<IAnglerPreferencesService>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly TripListViewModel Sut;

    protected BaseTripListViewModelTest()
    {
        MockConnectivity.IsOnline.Returns(true);
        MockTrips.LoadAsync(Arg.Any<CancellationToken>()).Returns(new TripHistoryModel([], new HashSet<Guid>(), false));
        MockParticipants.ListInvitationsAsync(Arg.Any<CancellationToken>())
            .Returns(FishingLogBook.Mobile.Api.Models.ApiResultModel<List<TripInvitationDto>>.Succeeded([]));
        Sut = new TripListViewModel(MockTrips, MockParticipants, MockPreferences, MockConnectivity, Text, new FixedTimeProvider(Now));
    }

    protected static TripSummaryDto Summary(Guid id, string status = TripConstants.Active, string role = TripParticipantConstants.Owner,
        DateTimeOffset? startedOn = null, int catches = 0, int photographs = 0, int notes = 0, string? title = null)
    {
        return new TripSummaryDto(id, status, startedOn ?? Now.AddHours(-2))
        {
            Role = role,
            Title = title,
            CatchCount = catches,
            PhotographCount = photographs,
            NoteCount = notes
        };
    }

    protected void ServeHistory(bool serverUnavailable = false, IReadOnlySet<Guid>? pending = null, params TripSummaryDto[] trips)
    {
        MockTrips.LoadAsync(Arg.Any<CancellationToken>()).Returns(new TripHistoryModel(trips, pending ?? new HashSet<Guid>(), serverUnavailable));
    }

    protected static LocalTripRecordModel Started(Guid id)
    {
        return new LocalTripRecordModel
        {
            UserId = OwnerUserId,
            Detail = new TripDetailDto(new TripViewDto(id, OwnerUserId, TripConstants.Active, Now)) { Role = TripParticipantConstants.Owner }
        };
    }

    protected static AnglerPreferencesModel PreferencesWithDefaultLocation(string name)
    {
        return AnglerPreferencesModel.Empty with
        {
            Locations = new FishingLocationPreferencesDto([new FishingLocationPreferenceDto(Guid.NewGuid(), name, IsDefault: true)])
        };
    }
}
