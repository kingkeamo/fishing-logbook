using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Commands.AssociateTripCatchesCommandTests;

public class WhenTestingHandle : BaseAssociateTripCatchesCommandTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        MockTripCatchService
            .AssociateAsync(Arg.Any<AssociateTripCatchesArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripCatchAssociationDto>(new TripNotFoundError()));

        // Act
        var response = await Sut.Handle(Command(), CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<TripNotFoundError>();
        response.Association.Should().BeNull();
        await MockTripCatchService.Received(1).AssociateAsync(
            Arg.Is<AssociateTripCatchesArgs>(args =>
                args.TripId == TripId
                && args.CatchIds.SequenceEqual(new[] { CatchId })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheAssociationWhenTheServiceSucceeds()
    {
        // Arrange
        var otherCatchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var association = new TripCatchAssociationDto([CatchId], [otherCatchId]);
        MockTripCatchService
            .AssociateAsync(Arg.Any<AssociateTripCatchesArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(association));

        // Act
        var response = await Sut.Handle(Command(CatchId, otherCatchId), CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Association.Should().Be(association);
        await MockTripCatchService.Received(1).AssociateAsync(
            Arg.Is<AssociateTripCatchesArgs>(args =>
                args.TripId == TripId
                && args.CatchIds.SequenceEqual(new[] { CatchId, otherCatchId })),
            Arg.Any<CancellationToken>());
    }
}
