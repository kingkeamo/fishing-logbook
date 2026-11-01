using System.Globalization;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Measurements;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripDetailViewModelTests;

public abstract class BaseTripDetailViewModelTest
{
    protected static readonly Guid ViewerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid FriendId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ITripService MockTrips = Substitute.For<ITripService>();
    protected readonly ICatchStore MockCatchStore = Substitute.For<ICatchStore>();
    protected readonly IPhotographFileStore MockFiles = Substitute.For<IPhotographFileStore>();
    protected readonly ITripPhotographService MockPhotographs = Substitute.For<ITripPhotographService>();
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IAccountUnitsService MockUnits = Substitute.For<IAccountUnitsService>();
    protected readonly TripDetailViewModel Sut;

    protected BaseTripDetailViewModelTest()
    {
        MockSession.Capture().Returns(new AccountSessionLeaseModel(ViewerId, 1, CancellationToken.None));
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        Sut = new TripDetailViewModel(
            MockTrips, new TripTimelineService(), MockCatchStore, MockFiles, MockPhotographs, MockSession, MockUnits, new MeasurementService(), Text, new FixedTimeProvider(Now));
    }

    protected static LocalTripRecordModel Trip(
        string status = TripConstants.Active,
        string role = TripParticipantConstants.Owner,
        Guid? owner = null,
        DateTimeOffset? endedOn = null,
        IReadOnlyList<TripNoteDto>? notes = null,
        IReadOnlyList<TripContributorDto>? contributors = null,
        IReadOnlyList<TripCatchSummaryDto>? catches = null,
        IReadOnlyList<TripPhotographViewDto>? photographs = null,
        string? title = null,
        string? place = null)
    {
        return new LocalTripRecordModel
        {
            UserId = ViewerId,
            Detail = new TripDetailDto(new TripViewDto(TripId, owner ?? ViewerId, status, Now.AddHours(-3), endedOn)
            {
                Title = title,
                PlaceName = place
            })
            {
                Role = role,
                Notes = notes ?? [],
                Contributors = contributors ?? [],
                Catches = catches ?? [],
                Photographs = photographs ?? []
            }
        };
    }

    protected async Task LoadAsync(LocalTripRecordModel trip)
    {
        MockTrips.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(trip);
        await Sut.LoadAsync(TripId, CancellationToken.None);
    }

    protected static TripNoteDto Note(Guid id, Guid by, string text = "Rising fish", DateTimeOffset? at = null)
    {
        return new TripNoteDto(id, TripId, text, at ?? Now.AddHours(-1)) { CreatedByUserId = by };
    }
}
