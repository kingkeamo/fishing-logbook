using System.Globalization;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripNoteEditorViewModelTests;

public abstract class BaseTripNoteEditorViewModelTest
{
    protected static readonly Guid NoteId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    protected static readonly DateTimeOffset Recorded = Now.AddHours(-1).AddSeconds(25);

    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ITripService MockTrips = Substitute.For<ITripService>();
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly TripNoteEditorViewModel Sut;

    protected void InitializeEditing(LocalTripRecordModel trip, TripNoteDto note)
    {
        Sut.Initialize(trip with { Detail = trip.Detail with { Notes = [note] } }, note.Id);
    }

    protected BaseTripNoteEditorViewModelTest()
    {
        MockSession.Capture().Returns(new AccountSessionLeaseModel(ViewerId, 1, CancellationToken.None));
        Sut = new TripNoteEditorViewModel(MockTrips, MockSession, Text, new FixedTimeProvider(Now));
    }
}
