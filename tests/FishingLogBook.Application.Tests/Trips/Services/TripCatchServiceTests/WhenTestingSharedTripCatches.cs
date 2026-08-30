using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripCatchServiceTests;

public class WhenTestingSharedTripCatches : BaseTripCatchServiceTest
{
    [Fact]
    public async Task ItShouldRefuseAnAnglerWhoIsNotOnTheSharedTrip()
    {
        // Arrange
        GivenNoTrip();
        GivenCatch(CatchId, CurrentUserId);

        // Act
        var result = await Sut.AssociateAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnotherAnglersCatchOnASharedTrip()
    {
        // Arrange
        GivenSharedTrip();
        GivenCatch(CatchId, OtherUserId);

        // Act
        var result = await Sut.AssociateAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AssociatedCatchIds.Should().BeEmpty();
        result.Value.RejectedCatchIds.Should().BeEquivalentTo([CatchId]);
        await MockCatchRepository.DidNotReceive().AssociateTripAsync(
            Arg.Any<PersistCatchTripArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAttachAParticipantsOwnCatchToTheSharedTripId()
    {
        // Arrange
        GivenSharedTrip();
        GivenCatch(CatchId, CurrentUserId);

        // Act
        var result = await Sut.AssociateAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AssociatedCatchIds.Should().BeEquivalentTo([CatchId]);
        await MockCatchRepository.Received(1).AssociateTripAsync(
            Arg.Is<PersistCatchTripArgs>(args =>
                args.CatchId == CatchId
                && args.TripId == TripId
                && args.UserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    private static AssociateTripCatchesArgs Args()
    {
        return new AssociateTripCatchesArgs
        {
            TripId = TripId,
            CatchIds = [CatchId]
        };
    }
}
