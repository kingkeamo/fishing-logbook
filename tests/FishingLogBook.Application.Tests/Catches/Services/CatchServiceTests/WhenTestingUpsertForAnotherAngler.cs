using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class WhenTestingUpsertForAnotherAngler : BaseCatchServiceTest
{
    private static readonly Guid RecorderUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CaughtByUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    [Fact]
    public async Task ItShouldRejectAnAnglerWhoIsNotAcceptedOnTheTrip()
    {
        // Arrange
        GivenRecorderCanContribute();
        MockTripAccessService
            .ResolveForAsync(TripId, CaughtByUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(Trip(), CaughtByUserId, participant: null)));

        // Act
        var result = await Sut.UpsertAsync(Args(CaughtByUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchAnglerNotEligibleError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAPendingParticipantAsTheSelectedAngler()
    {
        // Arrange
        GivenRecorderCanContribute();
        MockTripAccessService
            .ResolveForAsync(TripId, CaughtByUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(
                Trip(),
                CaughtByUserId,
                new TripParticipant
                {
                    Id = Guid.NewGuid(),
                    TripId = TripId,
                    UserId = CaughtByUserId,
                    Status = TripParticipantStatusEnum.Pending,
                    InvitedByUserId = Trip().OwnerUserId,
                    InvitedOn = StartedOn.AddDays(-1)
                })));

        // Act
        var result = await Sut.UpsertAsync(Args(CaughtByUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchAnglerNotEligibleError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectADeclinedParticipantAsTheSelectedAngler()
    {
        // Arrange
        GivenRecorderCanContribute();
        MockTripAccessService
            .ResolveForAsync(TripId, CaughtByUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(
                Trip(),
                CaughtByUserId,
                new TripParticipant
                {
                    Id = Guid.NewGuid(),
                    TripId = TripId,
                    UserId = CaughtByUserId,
                    Status = TripParticipantStatusEnum.Declined,
                    InvitedByUserId = Trip().OwnerUserId,
                    InvitedOn = StartedOn.AddDays(-1),
                    RespondedOn = StartedOn.AddHours(-1)
                })));

        // Act
        var result = await Sut.UpsertAsync(Args(CaughtByUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchAnglerNotEligibleError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectARemovedParticipantAsTheSelectedAngler()
    {
        // Arrange
        GivenRecorderCanContribute();
        MockTripAccessService
            .ResolveForAsync(TripId, CaughtByUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(
                Trip(),
                CaughtByUserId,
                new TripParticipant
                {
                    Id = Guid.NewGuid(),
                    TripId = TripId,
                    UserId = CaughtByUserId,
                    Status = TripParticipantStatusEnum.Accepted,
                    InvitedByUserId = Trip().OwnerUserId,
                    InvitedOn = StartedOn.AddDays(-1),
                    RespondedOn = StartedOn.AddHours(-1),
                    RemovedOn = StartedOn.AddHours(1)
                })));

        // Act
        var result = await Sut.UpsertAsync(Args(CaughtByUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchAnglerNotEligibleError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowTheTripOwnerAsTheSelectedAngler()
    {
        // Arrange
        var trip = Trip();
        GivenRecorderCanContribute();
        MockTripAccessService
            .ResolveForAsync(TripId, trip.OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(trip, trip.OwnerUserId, participant: null)));
        MockCatchRepository.UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(Args(trip.OwnerUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(saved =>
                saved.CaughtByUserId == trip.OwnerUserId
                && saved.CaughtByUserId == trip.OwnerUserId
                && saved.RecordedByUserId == RecorderUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordACatchForAnAcceptedParticipantWithTheRecorderDerivedFromAuthentication()
    {
        // Arrange
        GivenRecorderCanContribute();
        MockTripAccessService
            .ResolveForAsync(TripId, CaughtByUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(
                Trip(),
                CaughtByUserId,
                new TripParticipant
                {
                    Id = Guid.NewGuid(),
                    TripId = TripId,
                    UserId = CaughtByUserId,
                    Status = TripParticipantStatusEnum.Accepted,
                    InvitedByUserId = Trip().OwnerUserId,
                    InvitedOn = StartedOn.AddDays(-1),
                    RespondedOn = StartedOn.AddHours(-1)
                })));
        MockCatchRepository.UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        var args = Args(CaughtByUserId, spoofedRecorderUserId: Guid.NewGuid());

        // Act
        var result = await Sut.UpsertAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CaughtByUserId.Should().Be(CaughtByUserId);
        result.Value.CaughtByUserId.Should().Be(CaughtByUserId);
        result.Value.RecordedByUserId.Should().Be(RecorderUserId);
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(saved =>
                saved.CaughtByUserId == CaughtByUserId
                && saved.CaughtByUserId == CaughtByUserId
                && saved.RecordedByUserId == RecorderUserId),
            Arg.Any<CancellationToken>());
    }

    private void GivenRecorderCanContribute()
    {
        MockTripAccessService
            .ResolveForAsync(TripId, RecorderUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(
                Trip(),
                RecorderUserId,
                new TripParticipant
                {
                    Id = Guid.NewGuid(),
                    TripId = TripId,
                    UserId = RecorderUserId,
                    Status = TripParticipantStatusEnum.Accepted,
                    InvitedByUserId = Trip().OwnerUserId,
                    InvitedOn = StartedOn.AddDays(-1),
                    RespondedOn = StartedOn.AddHours(-1)
                })));
    }

    private static Trip Trip()
    {
        return new Trip
        {
            Id = TripId,
            OwnerUserId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Status = TripStatusEnum.Active,
            StartedOn = StartedOn
        };
    }

    private static UpsertCatchArgs Args(Guid selectedAnglerUserId, Guid? spoofedRecorderUserId = null)
    {
        var catchId = Guid.NewGuid();
        return new UpsertCatchArgs
        {
            UserId = RecorderUserId,
            Catch = new CatchDto(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [new CatchPhotographDto(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)])
            {
                TripId = TripId,
                CaughtByUserId = selectedAnglerUserId,
                RecordedByUserId = spoofedRecorderUserId ?? RecorderUserId
            }
        };
    }
}
