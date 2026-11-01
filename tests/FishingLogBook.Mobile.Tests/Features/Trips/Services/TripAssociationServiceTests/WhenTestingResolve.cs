using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripAssociationServiceTests;

public sealed class WhenTestingResolve : BaseTripServiceTest
{
    private static readonly Guid FriendId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid PendingId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly ITripParticipantClient _participants = Substitute.For<ITripParticipantClient>();

    private TripAssociationService Association => new(TripSut, TripStore, _participants, MockSessionCoordinator, MockConnectivity);

    private void ServeTrip(TripDetailDto detail)
    {
        MockClient.GetAsync(detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(detail));
    }

    private void ServeList(params TripDetailDto[] trips)
    {
        MockClient.ListAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<List<TripSummaryDto>>.Succeeded([.. trips.Select(Summary)]));
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenNoTripIsActive()
    {
        // Arrange
        ServeList();
        // Act
        var association = await Association.ResolveAsync(null, CancellationToken.None);
        // Assert
        association.Should().BeNull();
        await _participants.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOfferACompletedTripAsTheActiveTrip()
    {
        // Arrange
        ServeList(ServerTrip(Guid.NewGuid(), status: TripConstants.Completed));
        // Act
        var association = await Association.ResolveAsync(null, CancellationToken.None);
        // Assert
        association.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotOfferATripWhereTheAccountCannotContribute()
    {
        // Arrange
        var detail = ServerTrip(Guid.NewGuid(), role: TripParticipantConstants.None, owner: Guid.NewGuid());
        ServeList(detail);
        ServeTrip(detail);
        // Act
        var association = await Association.ResolveAsync(null, CancellationToken.None);
        // Assert
        association.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnNothingForARequestedTripThatIsNotAvailable()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        MockClient.GetAsync(tripId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Failed(ApiOutcomeEnum.Forbidden));
        // Act
        var association = await Association.ResolveAsync(tripId, CancellationToken.None);
        // Assert
        association.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldPreferTheTripTheAccountOwnsOverOneItOnlyParticipatesIn()
    {
        // Arrange
        var shared = ServerTrip(Guid.NewGuid(), role: TripParticipantConstants.Participant, owner: FriendId);
        var owned = ServerTrip(Guid.NewGuid());
        ServeList(shared, owned);
        ServeTrip(shared);
        ServeTrip(owned);
        _participants.GetAsync(owned.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        var association = await Association.ResolveAsync(null, CancellationToken.None);
        // Assert
        association!.TripId.Should().Be(owned.Trip.Id);
    }

    [Fact]
    public async Task ItShouldOnlyOfferTheAccountItselfWhenOfflineAndNotAskTheServerForParticipants()
    {
        // Arrange
        var detail = ServerTrip(Guid.NewGuid());
        ServeTrip(detail);
        await TripSut.GetAsync(detail.Trip.Id, CancellationToken.None);
        MockConnectivity.IsOnline.Returns(false);
        // Act
        var association = await Association.ResolveAsync(null, CancellationToken.None);
        // Assert
        association!.TripId.Should().Be(detail.Trip.Id);
        association.Anglers.Should().ContainSingle(angler => angler.UserId == OwnerUserId && angler.IsSelf);
        await _participants.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFallBackToTheAccountItselfWhenTheParticipantsCannotBeLoaded()
    {
        // Arrange
        var detail = ServerTrip(Guid.NewGuid());
        ServeList(detail);
        ServeTrip(detail);
        _participants.GetAsync(detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        var association = await Association.ResolveAsync(null, CancellationToken.None);
        // Assert
        association!.Anglers.Should().ContainSingle(angler => angler.IsSelf);
    }

    [Fact]
    public async Task ItShouldOfferOnlyTheOwnerAndAcceptedParticipantsAsAnglersWithTheAccountFirst()
    {
        // Arrange
        var detail = ServerTrip(Guid.NewGuid(), role: TripParticipantConstants.Participant, owner: FriendId);
        ServeList(detail);
        ServeTrip(detail);
        _participants.GetAsync(detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Succeeded(
            new TripParticipantsDto(detail.Trip.Id, TripParticipantConstants.Participant)
            {
                Participants =
                [
                    new TripParticipantDto(FriendId, TripParticipantConstants.Accepted, "Friend", null, Now) { IsOwner = true },
                    new TripParticipantDto(OwnerUserId, TripParticipantConstants.Accepted, "Me", null, Now),
                    new TripParticipantDto(PendingId, TripParticipantConstants.Pending, "Invited", null, Now)
                ]
            }));
        // Act
        var association = await Association.ResolveAsync(null, CancellationToken.None);
        // Assert
        association!.Anglers.Select(angler => (angler.UserId, angler.IsSelf)).Should()
            .Equal((OwnerUserId, true), (FriendId, false));
        association.Anglers[1].DisplayName.Should().Be("Friend");
    }

    [Fact]
    public async Task ItShouldReportAnUnsyncedLocalTripAsAvailableForAssociationWhileOffline()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var local = await TripSut.StartAsync("Local", null, CancellationToken.None);
        // Act
        var association = await Association.ResolveAsync(null, CancellationToken.None);
        var available = await Association.IsAvailableAsync(local.Detail.Trip.Id, CancellationToken.None);
        // Assert
        association!.TripId.Should().Be(local.Detail.Trip.Id);
        association.Title.Should().Be("Local");
        available.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldReportARevokedOrUnknownTripAsUnavailable()
    {
        // Arrange
        var detail = ServerTrip(Guid.NewGuid());
        ServeTrip(detail);
        await TripSut.GetAsync(detail.Trip.Id, CancellationToken.None);
        await TripStore.RevokeAsync(OwnerLease, detail.Trip.Id, CancellationToken.None);
        // Act
        var revoked = await Association.IsAvailableAsync(detail.Trip.Id, CancellationToken.None);
        var unknown = await Association.IsAvailableAsync(Guid.NewGuid(), CancellationToken.None);
        // Assert
        revoked.Should().BeFalse();
        unknown.Should().BeFalse();
    }
}
