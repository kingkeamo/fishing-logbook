using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripCatchServiceTests;

public class WhenTestingAssociate : BaseTripCatchServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheTripIsUnknown()
    {
        // Arrange
        GivenNoTrip();

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockCatchRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheTripBelongsToAnotherAngler()
    {
        // Arrange
        GivenTrip(OtherUserId);

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockCatchRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheCatchLookupFails()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        MockCatchRepository.GetByIdAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Catch?>("Failed to load the catch."));

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheAssociationCallFails()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenCatch(CatchId, CurrentUserId);
        MockCatchRepository.AssociateTripAsync(Arg.Any<PersistCatchTripArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<bool>("Failed to save the catch."));

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRejectACatchBelongingToAnotherAngler()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenCatch(CatchId, OtherUserId);

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AssociatedCatchIds.Should().BeEmpty();
        result.Value.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchAlreadyAssociatedWithAnotherTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenCatch(CatchId, CurrentUserId, tripId: Guid.NewGuid());

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchAlreadyAssociatedWithThisTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenCatch(CatchId, CurrentUserId, tripId: TripId);

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnknownCatch()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenNoCatch(CatchId);

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchCaughtBeforeTheTripStarted()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenCatch(CatchId, CurrentUserId, caughtOn: StartedOn.AddMinutes(-1));

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchCaughtAfterACompletedTripFinished()
    {
        // Arrange
        GivenTrip(CurrentUserId, TripStatusEnum.Completed);
        GivenCatch(CatchId, CurrentUserId, caughtOn: StartedOn.AddHours(3).AddMinutes(1));

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchCaughtWellIntoTheFutureOnAnActiveTrip()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenCatch(CatchId, CurrentUserId, caughtOn: DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreDuplicateCatchIdsInOneRequest()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenCatch(CatchId, CurrentUserId);

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId, CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AssociatedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
        await MockCatchRepository.Received(1).AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptACatchCaughtExactlyWhenTheTripStarted()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenCatch(CatchId, CurrentUserId, caughtOn: StartedOn);

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AssociatedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
    }

    [Fact]
    public async Task ItShouldAcceptACatchCaughtExactlyWhenACompletedTripFinished()
    {
        // Arrange
        var endedOn = StartedOn.AddHours(3);
        GivenTrip(CurrentUserId, TripStatusEnum.Completed);
        GivenCatch(CatchId, CurrentUserId, caughtOn: endedOn);

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AssociatedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
    }

    [Fact]
    public async Task ItShouldAssociateAnEligibleCatchUsingTheTrustedCurrentUser()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenCatch(CatchId, CurrentUserId);

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockCatchRepository.Received(1).AssociateTripAsync(
            Arg.Is<PersistCatchTripArgs>(args =>
                args.CatchId == CatchId
                && args.TripId == TripId
                && args.UserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAssociateEligibleCatchesAndRejectIneligibleOnesInOneRequest()
    {
        // Arrange
        GivenTrip(CurrentUserId);
        GivenCatch(CatchId, CurrentUserId);
        GivenCatch(OtherCatchId, OtherUserId);

        // Act
        var result = await Sut.AssociateAsync(Args(CatchId, OtherCatchId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AssociatedCatchIds.Should().ContainSingle().Which.Should().Be(CatchId);
        result.Value.RejectedCatchIds.Should().ContainSingle().Which.Should().Be(OtherCatchId);
        await MockCatchRepository.Received(1).AssociateTripAsync(
            Arg.Is<PersistCatchTripArgs>(args => args.CatchId == CatchId),
            Arg.Any<CancellationToken>());
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Is<PersistCatchTripArgs>(args => args.CatchId == OtherCatchId),
            Arg.Any<CancellationToken>());
    }

    private static AssociateTripCatchesArgs Args(params Guid[] catchIds)
    {
        return new AssociateTripCatchesArgs
        {
            TripId = TripId,
            CatchIds = catchIds
        };
    }
}
