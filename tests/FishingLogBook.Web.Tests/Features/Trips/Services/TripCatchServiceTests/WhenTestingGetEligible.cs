using AwesomeAssertions;
using FishingLogBook.Web.Features.Trips.Enums;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripCatchServiceTests;

public class WhenTestingGetEligible : BaseTripCatchServiceTest
{
    [Fact]
    public async Task ItShouldOfferACatchRecordedDuringACompletedTripThatIsNotOnATrip()
    {
        // Arrange
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([Catch(PikeCatchId, StartedOn.AddHours(3))]);

        // Act
        var eligible = await Sut.GetEligibleAsync(
            CompletedScope(),
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        eligible.Select(candidate => candidate.Id).Should().Equal(PikeCatchId);
        await MockCatchClient.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOfferACatchFromBeforeTheTripStarted()
    {
        // Arrange
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([Catch(PikeCatchId, StartedOn.AddMinutes(-1))]);

        // Act
        var eligible = await Sut.GetEligibleAsync(
            CompletedScope(),
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        eligible.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotOfferACatchFromAfterACompletedTripFinished()
    {
        // Arrange
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([Catch(PikeCatchId, EndedOn.AddMinutes(1))]);

        // Act
        var eligible = await Sut.GetEligibleAsync(
            CompletedScope(),
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        eligible.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldUseNowAsTheUpperBoundOfAnActiveTrip()
    {
        // Arrange
        var startedOn = DateTimeOffset.UtcNow.AddHours(-2);
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(
            [
                Catch(PikeCatchId, startedOn.AddMinutes(30)),
                Catch(TroutCatchId, DateTimeOffset.UtcNow.AddHours(1))
            ]);

        // Act
        var eligible = await Sut.GetEligibleAsync(
            ActiveScope(startedOn),
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        eligible.Select(candidate => candidate.Id).Should().Equal(PikeCatchId);
    }

    [Fact]
    public async Task ItShouldNotOfferACatchAlreadyOnThisTrip()
    {
        // Arrange
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([Catch(PikeCatchId, StartedOn.AddHours(1), tripId: TripId)]);

        // Act
        var eligible = await Sut.GetEligibleAsync(
            CompletedScope(),
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        eligible.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotOfferACatchAlreadyOnAnotherTrip()
    {
        // Arrange
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([Catch(PikeCatchId, StartedOn.AddHours(1), tripId: OtherTripId)]);

        // Act
        var eligible = await Sut.GetEligibleAsync(
            CompletedScope(),
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        eligible.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotOfferACatchBelongingToAnotherAngler()
    {
        // Arrange
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns([Catch(PikeCatchId, StartedOn.AddHours(1), userId: OtherUserId)]);

        // Act
        var eligible = await Sut.GetEligibleAsync(
            CompletedScope(),
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        eligible.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReadTheServerForAHistoricalTripAndApplyTheSameRules()
    {
        // Arrange
        MockCatchClient.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(
            [
                RemoteCatch(PikeCatchId, StartedOn.AddHours(2)),
                RemoteCatch(TroutCatchId, StartedOn.AddHours(3), tripId: OtherTripId),
                RemoteCatch(Guid.NewGuid(), EndedOn.AddHours(1)),
                RemoteCatch(Guid.NewGuid(), StartedOn.AddHours(1), userId: OtherUserId)
            ]);

        // Act
        var eligible = await Sut.GetEligibleAsync(
            CompletedScope(),
            TripStorageEnum.Server,
            CancellationToken.None);

        // Assert
        eligible.Select(candidate => candidate.Id).Should().Equal(PikeCatchId);
        await MockCatchStore.DidNotReceive().GetMetadataAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOrderTheCandidatesByWhenTheyWereCaught()
    {
        // Arrange
        MockCatchStore.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(
            [
                Catch(TroutCatchId, StartedOn.AddHours(4)),
                Catch(PikeCatchId, StartedOn.AddHours(1))
            ]);

        // Act
        var eligible = await Sut.GetEligibleAsync(
            CompletedScope(),
            TripStorageEnum.LocalFirst,
            CancellationToken.None);

        // Assert
        eligible.Select(candidate => candidate.Id).Should().Equal(PikeCatchId, TroutCatchId);
    }
}
