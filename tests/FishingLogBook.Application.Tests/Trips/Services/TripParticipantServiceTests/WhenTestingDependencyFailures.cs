using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Tests.Common;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripParticipantServiceTests;

public class WhenTestingDependencyFailures : BaseTripParticipantServiceTest
{
    private static InviteTripParticipantArgs Invite()
    {
        return new InviteTripParticipantArgs { TripId = TripId, InvitedUserId = InvitedUserId };
    }

    private static RemoveTripParticipantArgs Remove()
    {
        return new RemoveTripParticipantArgs { TripId = TripId, ParticipantUserId = InvitedUserId };
    }

    private static RespondToTripInvitationArgs Respond()
    {
        return new RespondToTripInvitationArgs { TripId = TripId, Response = TripParticipantStatusEnum.Accepted };
    }

    [Fact]
    public async Task ItShouldReturnTheOwnerCheckFailureWhenInviting()
    {
        // Arrange
        MockTripAccessService.RequireOwnerAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripAccess>("The access check failed."));

        // Act
        var result = await Sut.InviteAsync(Invite(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The access check failed.");
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheUserLookupFailureWhenInviting()
    {
        // Arrange
        MockProfileRepository.UserExistsAsync(InvitedUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<bool>("The user lookup failed."));

        // Act
        var result = await Sut.InviteAsync(Invite(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The user lookup failed.");
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheMembershipLookupFailureWhenInviting()
    {
        // Arrange
        MockTripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripParticipant?>("The membership lookup failed."));

        // Act
        var result = await Sut.InviteAsync(Invite(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The membership lookup failed.");
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheSaveFailureWhenInviting()
    {
        // Arrange
        MockTripParticipantRepository.UpsertAsync(Arg.Any<TripParticipant>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripParticipant>("The save failed."));

        // Act
        var result = await Sut.InviteAsync(Invite(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The save failed.");
        await MockAnglerLookupService.DidNotReceive().DescribeAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheOwnerCheckFailureWhenRemoving()
    {
        // Arrange
        MockTripAccessService.RequireOwnerAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripAccess>("The access check failed."));

        // Act
        var result = await Sut.RemoveAsync(Remove(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The access check failed.");
        await MockTripParticipantRepository.DidNotReceive().FindAsync(
            Arg.Any<FindTripParticipantArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheMembershipLookupFailureWhenRemoving()
    {
        // Arrange
        MockTripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripParticipant?>("The membership lookup failed."));

        // Act
        var result = await Sut.RemoveAsync(Remove(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The membership lookup failed.");
        await MockTripParticipantRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripParticipant>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheSaveFailureWhenRemoving()
    {
        // Arrange
        GivenExistingMembership(InvitedUserId, TripParticipantStatusEnum.Accepted);
        MockTripParticipantRepository.UpsertAsync(Arg.Any<TripParticipant>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripParticipant>("The save failed."));

        // Act
        var result = await Sut.RemoveAsync(Remove(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The save failed.");
        await MockAnglerLookupService.DidNotReceive().DescribeAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheMembershipLookupFailureWhenResponding()
    {
        // Arrange
        MockTripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripParticipant?>("The membership lookup failed."));

        // Act
        var result = await Sut.RespondAsync(Respond(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The membership lookup failed.");
        await MockTripRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheSaveFailureWhenResponding()
    {
        // Arrange
        GivenExistingMembership(CurrentUserId, TripParticipantStatusEnum.Pending);
        MockTripParticipantRepository.UpsertAsync(Arg.Any<TripParticipant>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripParticipant>("The save failed."));

        // Act
        var result = await Sut.RespondAsync(Respond(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The save failed.");
    }

    [Fact]
    public async Task ItShouldReturnTheParticipantListFailureWhenReading()
    {
        // Arrange
        MockTripParticipantRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<TripParticipant>>("The participant list failed."));

        // Act
        var result = await Sut.GetAsync(new GetTripParticipantsArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The participant list failed.");
        await MockAnglerLookupService.DidNotReceive().DescribeAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheAnglerDescriptionFailureWhenReading()
    {
        // Arrange
        MockAnglerLookupService
            .DescribeAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyDictionary<Guid, AnglerSummaryDto>>("The description failed."));

        // Act
        var result = await Sut.GetAsync(new GetTripParticipantsArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The description failed.");
    }

    [Fact]
    public async Task ItShouldReturnThePendingInvitationLookupFailure()
    {
        // Arrange
        MockTripParticipantRepository
            .GetPendingInvitationsByUserIdAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<TripParticipant>>("The invitation lookup failed."));

        // Act
        var result = await Sut.GetMyInvitationsAsync(CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The invitation lookup failed.");
        await MockTripRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheOwnerDescriptionFailureWhileListingInvitations()
    {
        // Arrange
        MockTripParticipantRepository
            .GetPendingInvitationsByUserIdAsync(CurrentUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>(
                [Membership(CurrentUserId, TripParticipantStatusEnum.Pending)]));
        MockAnglerLookupService
            .DescribeAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyDictionary<Guid, AnglerSummaryDto>>("The description failed."));

        // Act
        var result = await Sut.GetMyInvitationsAsync(CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The description failed.");
    }
}
