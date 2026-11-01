using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.PlannedTripServiceTests;

public abstract class BasePlannedTripServiceTest : BaseTripServiceTest
{
    protected static readonly Guid FriendUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected readonly PlannedTripService Sut;

    protected BasePlannedTripServiceTest()
    {
        MockClient.ListPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<List<PlannedTripOptionDto>>.Succeeded([]));
        Sut = new PlannedTripService(TripStore, MockClient, TripSut, MockSessionCoordinator, MockConnectivity, Clock);
    }

    protected static PlannedTripOptionDto ServerOption(Guid id, DateTimeOffset plannedStart, bool organiser = false)
    {
        return new PlannedTripOptionDto(id, plannedStart, organiser ? OwnerUserId : FriendUserId, "Sam")
        {
            Title = "Lough Mask",
            Role = organiser ? TripParticipantConstants.Owner : TripParticipantConstants.Participant,
            Participants =
            [
                new PlannedTripParticipantDto(OwnerUserId, "Me", TripParticipantConstants.Accepted),
                new PlannedTripParticipantDto(Guid.NewGuid(), "Pat", TripParticipantConstants.Pending)
            ]
        };
    }

    protected void ServePlanned(params PlannedTripOptionDto[] options)
    {
        MockClient.ListPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<List<PlannedTripOptionDto>>.Succeeded([.. options]));
    }

    protected void FailPlannedRead()
    {
        MockClient.ListPlannedAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<List<PlannedTripOptionDto>>.Failed(ApiOutcomeEnum.Unavailable));
    }
}
