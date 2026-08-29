using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripParticipantRepositoryTests;

public class WhenTestingUpsert : BaseTripParticipantRepositoryTest
{
    public WhenTestingUpsert(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldFailWhenTheTripDoesNotExist()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var invitedUserId = await CreateUserAsync();

        // Act
        var result = await Sut.UpsertAsync(
            NewParticipant(Guid.NewGuid(), invitedUserId, ownerUserId),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the trip participant.");
    }

    [Fact]
    public async Task ItShouldFailWhenTheInvitedAnglerDoesNotExist()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);

        // Act
        var result = await Sut.UpsertAsync(
            NewParticipant(tripId, Guid.NewGuid(), ownerUserId),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the trip participant.");
    }

    [Fact]
    public async Task ItShouldRefuseAnAnglerInvitingThemselves()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);

        // Act
        var result = await Sut.UpsertAsync(
            NewParticipant(tripId, ownerUserId, ownerUserId),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the trip participant.");
    }

    [Fact]
    public async Task ItShouldRefuseAnUnknownStatus()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var invitedUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);
        var participant = NewParticipant(tripId, invitedUserId, ownerUserId);

        // Act
        var result = await Sut.UpsertAsync(
            new Domain.Trips.TripParticipant
            {
                Id = participant.Id,
                TripId = tripId,
                UserId = invitedUserId,
                Status = (TripParticipantStatusEnum)42,
                InvitedByUserId = ownerUserId,
                InvitedOn = participant.InvitedOn
            },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save the trip participant.");
    }

    [Fact]
    public async Task ItShouldKeepOneMembershipRowWhenTheSameAnglerIsReinvited()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var invitedUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);
        var first = NewParticipant(tripId, invitedUserId, ownerUserId);
        await Sut.UpsertAsync(first, CancellationToken.None);

        // Act
        var second = await Sut.UpsertAsync(
            NewParticipant(tripId, invitedUserId, ownerUserId, TripParticipantStatusEnum.Declined),
            CancellationToken.None);

        // Assert
        second.IsSuccess.Should().BeTrue();
        var participants = await Sut.GetByTripIdAsync(tripId, CancellationToken.None);
        participants.Value.Should().ContainSingle();
        participants.Value[0].Status.Should().Be(TripParticipantStatusEnum.Declined);
    }

    [Fact]
    public async Task ItShouldRoundTripTheAcceptedMembership()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var invitedUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);

        // Act
        var result = await Sut.UpsertAsync(
            NewParticipant(tripId, invitedUserId, ownerUserId, TripParticipantStatusEnum.Accepted),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TripId.Should().Be(tripId);
        result.Value.UserId.Should().Be(invitedUserId);
        result.Value.InvitedByUserId.Should().Be(ownerUserId);
        result.Value.Status.Should().Be(TripParticipantStatusEnum.Accepted);
        result.Value.IsContributing.Should().BeTrue();
        result.Value.RemovedOn.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldPersistARemovalWithoutLosingTheMembershipHistory()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var invitedUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);
        var accepted = NewParticipant(
            tripId,
            invitedUserId,
            ownerUserId,
            TripParticipantStatusEnum.Accepted);
        await Sut.UpsertAsync(accepted, CancellationToken.None);

        // Act
        var removed = await Sut.UpsertAsync(
            accepted.RemovedAt(StartedOn.AddHours(2)),
            CancellationToken.None);

        // Assert
        removed.IsSuccess.Should().BeTrue();
        removed.Value.RemovedOn.Should().Be(StartedOn.AddHours(2));
        removed.Value.IsContributing.Should().BeFalse();
        var found = await Sut.FindAsync(
            new FindTripParticipantArgs { TripId = tripId, UserId = invitedUserId },
            CancellationToken.None);
        found.Value!.Status.Should().Be(TripParticipantStatusEnum.Accepted);
        found.Value.RespondedOn.Should().NotBeNull();
    }
}
