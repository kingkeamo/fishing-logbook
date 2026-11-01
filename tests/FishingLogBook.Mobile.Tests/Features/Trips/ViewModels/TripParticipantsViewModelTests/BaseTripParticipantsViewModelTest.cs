using System.Globalization;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripParticipantsViewModelTests;

public abstract class BaseTripParticipantsViewModelTest
{
    protected static readonly Guid ViewerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid FriendId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid TripId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    protected static readonly Guid PendingId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    protected static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly ITripParticipantClient MockParticipants = Substitute.For<ITripParticipantClient>();
    protected readonly IAnglerLookupClient MockLookup = Substitute.For<IAnglerLookupClient>();
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly TripParticipantsViewModel Sut;

    protected BaseTripParticipantsViewModelTest()
    {
        MockConnectivity.IsOnline.Returns(true);
        MockSession.Capture().Returns(new AccountSessionLeaseModel(ViewerId, 1, CancellationToken.None));
        Sut = new TripParticipantsViewModel(MockParticipants, MockLookup, MockConnectivity, MockSession, Text);
    }

    protected static TripParticipantsDto Participants(string role, params TripParticipantDto[] participants)
    {
        return new TripParticipantsDto(TripId, role) { Participants = participants };
    }

    protected static TripParticipantDto Owner() => new(ViewerId, TripParticipantConstants.Accepted, "Me", null, Now) { IsOwner = true };

    protected static TripParticipantDto Friend(string status = TripParticipantConstants.Accepted) => new(FriendId, status, "Sam", null, Now);

    protected void ServeParticipants(TripParticipantsDto dto)
    {
        MockParticipants.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Succeeded(dto));
    }
}
