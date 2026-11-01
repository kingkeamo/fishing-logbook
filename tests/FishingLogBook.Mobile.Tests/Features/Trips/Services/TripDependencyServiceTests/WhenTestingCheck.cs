using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripDependencyServiceTests;

public sealed class WhenTestingCheck : BaseTripServiceTest
{
    private TripDependencyService Dependency => new(TripStore, MockClient, OutboxStore);

    [Fact]
    public async Task ItShouldFailTerminallyWhenTheServerDeniesAccessToTheTrip()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        MockClient.GetAsync(tripId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Failed(ApiOutcomeEnum.Forbidden));
        // Act
        var result = await Dependency.CheckAsync(OwnerUserId, tripId, CancellationToken.None);
        // Assert
        result!.Outcome.Should().Be(OutboxOperationOutcomeEnum.Terminal);
        result.ErrorCode.Should().Be("TripUnavailable");
    }

    [Fact]
    public async Task ItShouldFailTerminallyWhenTheTripNoLongerExistsOnTheServer()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        MockClient.GetAsync(tripId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Failed(ApiOutcomeEnum.NotFound));
        // Act
        var result = await Dependency.CheckAsync(OwnerUserId, tripId, CancellationToken.None);
        // Assert
        result!.Outcome.Should().Be(OutboxOperationOutcomeEnum.Terminal);
        result.ErrorCode.Should().Be("TripUnavailable");
    }

    [Fact]
    public async Task ItShouldFailTerminallyWhenTheAccountIsNoLongerAParticipant()
    {
        // Arrange
        var detail = ServerTrip(Guid.NewGuid(), role: TripParticipantConstants.None, owner: Guid.NewGuid());
        MockClient.GetAsync(detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(detail));
        // Act
        var result = await Dependency.CheckAsync(OwnerUserId, detail.Trip.Id, CancellationToken.None);
        // Assert
        result!.Outcome.Should().Be(OutboxOperationOutcomeEnum.Terminal);
    }

    [Fact]
    public async Task ItShouldRetryWhenTheServerCannotBeReached()
    {
        // Arrange
        var tripId = Guid.NewGuid();
        MockClient.GetAsync(tripId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        var result = await Dependency.CheckAsync(OwnerUserId, tripId, CancellationToken.None);
        // Assert
        result!.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
    }

    [Fact]
    public async Task ItShouldWaitForALocallyCreatedTripToReachTheServerFirstWithoutCallingTheServer()
    {
        // Arrange
        var trip = await TripSut.StartAsync("Local", null, CancellationToken.None);
        // Act
        var result = await Dependency.CheckAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None);
        // Assert
        result!.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
        result.ErrorCode.Should().Be("WaitingForTrip");
        await MockClient.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailTerminallyWhenAnAlreadyRevokedTripIsReferenced()
    {
        // Arrange
        var detail = ServerTrip(Guid.NewGuid());
        MockClient.GetAsync(detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(detail));
        await TripSut.GetAsync(detail.Trip.Id, CancellationToken.None);
        await TripStore.RevokeAsync(OwnerLease, detail.Trip.Id, CancellationToken.None);
        MockClient.ClearReceivedCalls();
        // Act
        var result = await Dependency.CheckAsync(OwnerUserId, detail.Trip.Id, CancellationToken.None);
        // Assert
        result!.Outcome.Should().Be(OutboxOperationOutcomeEnum.Terminal);
        await MockClient.DidNotReceive().GetAsync(detail.Trip.Id, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TripParticipantConstants.Owner)]
    [InlineData(TripParticipantConstants.Participant)]
    public async Task ItShouldAllowACatchWhenTheServerConfirmsTheAccountCanContribute(string role)
    {
        // Arrange
        var detail = ServerTrip(Guid.NewGuid(), role: role);
        MockClient.GetAsync(detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(detail));
        // Act
        var result = await Dependency.CheckAsync(OwnerUserId, detail.Trip.Id, CancellationToken.None);
        // Assert
        result.Should().BeNull();
        await MockClient.Received(1).GetAsync(detail.Trip.Id, Arg.Any<CancellationToken>());
    }
}
