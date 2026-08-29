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

public class WhenTestingUpsertAnglerCorrection : BaseCatchServiceTest
{
    private static readonly Guid RecorderUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OriginalAnglerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CorrectedAnglerUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TripOwnerUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    [Fact]
    public async Task ItShouldRejectCorrectingTheAnglerToAParticipantWhoIsNotAccepted()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatchRecordedByMyselfForMyself(catchId);
        GivenRecorderCanContributeToTheTrip();
        MockTripAccessService
            .ResolveForAsync(TripId, CorrectedAnglerUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(Trip(), CorrectedAnglerUserId, participant: null)));

        // Act
        var result = await Sut.UpsertAsync(EditArgs(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchAnglerNotEligibleError>();
        await MockCatchRepository.DidNotReceive().UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCorrectTheAnglerToAnAcceptedParticipantWithoutRewritingTheRecorder()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatchRecordedByMyselfForMyself(catchId);
        GivenRecorderCanContributeToTheTrip();
        MockTripAccessService
            .ResolveForAsync(TripId, CorrectedAnglerUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(
                Trip(),
                CorrectedAnglerUserId,
                new TripParticipant
                {
                    Id = Guid.NewGuid(),
                    TripId = TripId,
                    UserId = CorrectedAnglerUserId,
                    Status = TripParticipantStatusEnum.Accepted,
                    InvitedByUserId = TripOwnerUserId,
                    InvitedOn = StartedOn.AddDays(-1),
                    RespondedOn = StartedOn.AddHours(-1)
                })));
        MockCatchRepository.UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(EditArgs(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(CorrectedAnglerUserId);
        result.Value.AnglerUserId.Should().Be(CorrectedAnglerUserId);
        result.Value.RecordedByUserId.Should().Be(RecorderUserId);
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(saved =>
                saved.Id == catchId
                && saved.UserId == CorrectedAnglerUserId
                && saved.AnglerUserId == CorrectedAnglerUserId
                && saved.RecordedByUserId == RecorderUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheExistingAnglerWhenTheClientDoesNotRequestAChange()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatchRecordedByMyselfForMyself(catchId);
        GivenRecorderCanContributeToTheTrip();
        MockCatchRepository.UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));

        // Act
        var result = await Sut.UpsertAsync(EditArgs(catchId, selectedAnglerUserId: null), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockCatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(saved =>
                saved.Id == catchId
                && saved.UserId == OriginalAnglerUserId
                && saved.AnglerUserId == OriginalAnglerUserId
                && saved.RecordedByUserId == RecorderUserId),
            Arg.Any<CancellationToken>());
        await MockTripAccessService.DidNotReceive().ResolveForAsync(
            TripId,
            Arg.Is<Guid>(userId => userId != RecorderUserId),
            Arg.Any<CancellationToken>());
    }

    private void GivenExistingCatchRecordedByMyselfForMyself(Guid catchId)
    {
        MockCurrentUser.UserId.Returns(RecorderUserId);
        MockCatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(new Catch
            {
                Id = catchId,
                UserId = OriginalAnglerUserId,
                AnglerUserId = OriginalAnglerUserId,
                RecordedByUserId = RecorderUserId,
                TripId = TripId,
                CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z")
            }));
    }

    private void GivenRecorderCanContributeToTheTrip()
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
                    InvitedByUserId = TripOwnerUserId,
                    InvitedOn = StartedOn.AddDays(-1),
                    RespondedOn = StartedOn.AddHours(-1)
                })));
    }

    private static Trip Trip()
    {
        return new Trip
        {
            Id = TripId,
            OwnerUserId = TripOwnerUserId,
            Status = TripStatusEnum.Active,
            StartedOn = StartedOn
        };
    }

    private static UpsertCatchArgs EditArgs(Guid catchId, Guid? selectedAnglerUserId)
    {
        var catchDto = new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T09:00:00Z"),
            [new CatchPhotographDto(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)])
        {
            TripId = TripId,
            SpeciesName = "Pike"
        };
        if (selectedAnglerUserId is { } angler)
        {
            catchDto = catchDto with { AnglerUserId = angler };
        }

        return new UpsertCatchArgs
        {
            UserId = RecorderUserId,
            Catch = catchDto
        };
    }
}
