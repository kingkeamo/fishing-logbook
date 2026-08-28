using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Trips.Enums;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripCatchServiceTests;

public class WhenTestingAssociate : BaseTripCatchServiceTest
{
    [Fact]
    public async Task ItShouldDoNothingWhenNothingWasSelected()
    {
        // Act
        var association = await Sut.AssociateAsync(
            CompletedScope(),
            [],
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        association.AssociatedCatchIds.Should().BeEmpty();
        await MockCatchStore.DidNotReceive().UpdateTripAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().AssociateCatchesAsync(
            Arg.Any<Guid>(),
            Arg.Any<AssociateTripCatchesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectALocalCatchThatIsNotEligible()
    {
        // Arrange
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([Catch(PikeCatchId, StartedOn.AddHours(1))]);

        // Act
        var association = await Sut.AssociateAsync(
            CompletedScope(),
            [PikeCatchId, TroutCatchId],
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        association.AssociatedCatchIds.Should().Equal(PikeCatchId);
        association.RejectedCatchIds.Should().Equal(TroutCatchId);
        await MockCatchStore.Received(1).UpdateTripAsync(
            OwnerUserId,
            PikeCatchId,
            TripId,
            Arg.Any<CancellationToken>());
        await MockCatchStore.DidNotReceive().UpdateTripAsync(
            Arg.Any<Guid>(),
            TroutCatchId,
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().AssociateCatchesAsync(
            Arg.Any<Guid>(),
            Arg.Any<AssociateTripCatchesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAssociateADuplicateLocalCatchOnlyOnce()
    {
        // Arrange
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([Catch(PikeCatchId, StartedOn.AddHours(1))]);

        // Act
        var association = await Sut.AssociateAsync(
            CompletedScope(),
            [PikeCatchId, PikeCatchId],
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        association.AssociatedCatchIds.Should().Equal(PikeCatchId);
        await MockCatchStore.Received(1).UpdateTripAsync(
            OwnerUserId,
            PikeCatchId,
            TripId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAssociateALocalTripThroughTheOfflineStore()
    {
        // Arrange
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(
            [
                Catch(PikeCatchId, StartedOn.AddHours(1)),
                Catch(TroutCatchId, StartedOn.AddHours(2))
            ]);

        // Act
        var association = await Sut.AssociateAsync(
            CompletedScope(),
            [PikeCatchId, TroutCatchId],
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        association.AssociatedCatchIds.Should().Equal(PikeCatchId, TroutCatchId);
        association.RejectedCatchIds.Should().BeEmpty();
        await MockCatchStore.Received(1).UpdateTripAsync(
            OwnerUserId,
            PikeCatchId,
            TripId,
            Arg.Any<CancellationToken>());
        await MockCatchStore.Received(1).UpdateTripAsync(
            OwnerUserId,
            TroutCatchId,
            TripId,
            Arg.Any<CancellationToken>());
        await MockTripClient.DidNotReceive().AssociateCatchesAsync(
            Arg.Any<Guid>(),
            Arg.Any<AssociateTripCatchesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAssociateAHistoricalTripInOneAuthenticatedCall()
    {
        // Act
        var association = await Sut.AssociateAsync(
            CompletedScope(),
            [PikeCatchId, TroutCatchId],
            TripStorageEnum.Server,
            CancellationToken.None);

        // Assert
        association.AssociatedCatchIds.Should().Equal(PikeCatchId, TroutCatchId);
        await MockTripClient.Received(1).AssociateCatchesAsync(
            TripId,
            Arg.Is<AssociateTripCatchesDto>(request =>
                request.CatchIds.Count == 2
                && request.CatchIds.Contains(PikeCatchId)
                && request.CatchIds.Contains(TroutCatchId)),
            Arg.Any<CancellationToken>());
        await MockCatchStore.DidNotReceive().UpdateTripAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportTheCatchesTheServerRefused()
    {
        // Arrange
        MockTripClient.AssociateCatchesAsync(
                Arg.Any<Guid>(),
                Arg.Any<AssociateTripCatchesDto>(),
                Arg.Any<CancellationToken>())
            .Returns(new TripCatchAssociationDto([PikeCatchId], [TroutCatchId]));

        // Act
        var association = await Sut.AssociateAsync(
            CompletedScope(),
            [PikeCatchId, TroutCatchId],
            TripStorageEnum.Server,
            CancellationToken.None);

        // Assert
        association.AssociatedCatchIds.Should().Equal(PikeCatchId);
        association.RejectedCatchIds.Should().Equal(TroutCatchId);
        await MockCatchStore.DidNotReceive().UpdateTripAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotWriteLocallyWhenTheHistoricalCallFails()
    {
        // Arrange
        MockTripClient.AssociateCatchesAsync(
                Arg.Any<Guid>(),
                Arg.Any<AssociateTripCatchesDto>(),
                Arg.Any<CancellationToken>())
            .Returns<TripCatchAssociationDto?>(_ => throw new HttpRequestException("offline"));

        // Act
        var associate = async () => await Sut.AssociateAsync(
            CompletedScope(),
            [PikeCatchId],
            TripStorageEnum.Server,
            CancellationToken.None);

        // Assert
        await associate.Should().ThrowAsync<HttpRequestException>();
        await MockCatchStore.DidNotReceive().UpdateTripAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }
}
