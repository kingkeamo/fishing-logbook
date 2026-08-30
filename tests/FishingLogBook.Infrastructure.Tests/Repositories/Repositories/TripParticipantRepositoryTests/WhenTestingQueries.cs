using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.TripParticipantRepositoryTests;

public class WhenTestingQueries : BaseTripParticipantRepositoryTest
{
    public WhenTestingQueries(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldFindNothingForAnAnglerWhoWasNeverInvited()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);

        // Act
        var result = await Sut.FindAsync(
            new FindTripParticipantArgs { TripId = tripId, UserId = Guid.NewGuid() },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldListOnlyThePendingInvitationsOfThatAngler()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var invitedUserId = await CreateUserAsync();
        var otherInvitedUserId = await CreateUserAsync();
        var pendingTripId = await CreateTripAsync(ownerUserId);
        var acceptedTripId = await CreateTripAsync(ownerUserId);
        var othersTripId = await CreateTripAsync(ownerUserId);
        await Sut.UpsertAsync(
            NewParticipant(pendingTripId, invitedUserId, ownerUserId),
            CancellationToken.None);
        await Sut.UpsertAsync(
            NewParticipant(acceptedTripId, invitedUserId, ownerUserId, TripParticipantStatusEnum.Accepted),
            CancellationToken.None);
        await Sut.UpsertAsync(
            NewParticipant(othersTripId, otherInvitedUserId, ownerUserId),
            CancellationToken.None);

        // Act
        var result = await Sut.GetPendingInvitationsByUserIdAsync(invitedUserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].TripId.Should().Be(pendingTripId);
    }

    [Fact]
    public async Task ItShouldReturnEveryMembershipRowOfOneTrip()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var acceptedUserId = await CreateUserAsync();
        var declinedUserId = await CreateUserAsync();
        var tripId = await CreateTripAsync(ownerUserId);
        var otherTripId = await CreateTripAsync(ownerUserId);
        await Sut.UpsertAsync(
            NewParticipant(tripId, acceptedUserId, ownerUserId, TripParticipantStatusEnum.Accepted),
            CancellationToken.None);
        await Sut.UpsertAsync(
            NewParticipant(tripId, declinedUserId, ownerUserId, TripParticipantStatusEnum.Declined),
            CancellationToken.None);
        await Sut.UpsertAsync(
            NewParticipant(otherTripId, acceptedUserId, ownerUserId),
            CancellationToken.None);

        // Act
        var result = await Sut.GetByTripIdAsync(tripId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(participant => participant.UserId)
            .Should().BeEquivalentTo([acceptedUserId, declinedUserId]);
    }
}
