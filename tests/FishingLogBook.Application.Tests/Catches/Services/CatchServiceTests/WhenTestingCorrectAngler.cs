using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class WhenTestingCorrectAngler : BaseCatchServiceTest
{
    private static readonly Guid RecorderUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OriginalAnglerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CorrectedAnglerUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ThirdPartyUserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TripOwnerUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    [Fact]
    public async Task ItShouldRejectAThirdPartyWhoIsNeitherAnglerNorRecorder()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId);
        MockCurrentUser.UserId.Returns(ThirdPartyUserId);

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchEditNotPermittedError>();
        await MockCatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchThatIsNotAttachedToATrip()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        MockCurrentUser.UserId.Returns(RecorderUserId);
        MockCatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(new Catch
            {
                Id = catchId,
                CaughtByUserId = OriginalAnglerUserId,
                RecordedByUserId = RecorderUserId,
                TripId = null,
                CaughtOn = StartedOn
            }));

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchNotOnTripError>();
        await MockCatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAPendingParticipantAsTheCorrectedAngler()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId);
        MockCurrentUser.UserId.Returns(RecorderUserId);
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
                    Status = TripParticipantStatusEnum.Pending,
                    InvitedByUserId = TripOwnerUserId,
                    InvitedOn = StartedOn.AddDays(-1)
                })));

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchAnglerNotEligibleError>();
        await MockCatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectADeclinedParticipantAsTheCorrectedAngler()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId);
        MockCurrentUser.UserId.Returns(RecorderUserId);
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
                    Status = TripParticipantStatusEnum.Declined,
                    InvitedByUserId = TripOwnerUserId,
                    InvitedOn = StartedOn.AddDays(-1),
                    RespondedOn = StartedOn.AddHours(-1)
                })));

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchAnglerNotEligibleError>();
        await MockCatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectARemovedParticipantAsTheCorrectedAngler()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId);
        MockCurrentUser.UserId.Returns(RecorderUserId);
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
                    RespondedOn = StartedOn.AddHours(-1),
                    RemovedOn = StartedOn.AddHours(1)
                })));

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchAnglerNotEligibleError>();
        await MockCatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnrelatedUserAsTheCorrectedAngler()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId);
        MockCurrentUser.UserId.Returns(RecorderUserId);
        MockTripAccessService
            .ResolveForAsync(TripId, CorrectedAnglerUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(Trip(), CorrectedAnglerUserId, participant: null)));

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CatchAnglerNotEligibleError>();
        await MockCatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowTheRecorderToCorrectTheAnglerToAnAcceptedParticipant()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId);
        MockCurrentUser.UserId.Returns(RecorderUserId);
        GivenTheCorrectedAnglerIsAnAcceptedParticipant();
        MockCatchRepository.CorrectAnglerAsync(Arg.Any<PersistCatchAnglerArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        GivenTheRefreshedDetailIsReturned(catchId);

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CaughtByUserId.Should().Be(CorrectedAnglerUserId);
        result.Value.RecordedByUserId.Should().Be(RecorderUserId);
        await MockCatchRepository.Received(1).CorrectAnglerAsync(
            Arg.Is<PersistCatchAnglerArgs>(args =>
                args.CatchId == catchId && args.CaughtByUserId == CorrectedAnglerUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowTheAnglerToCorrectTheAnglerToAnAcceptedParticipant()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId);
        MockCurrentUser.UserId.Returns(OriginalAnglerUserId);
        GivenTheCorrectedAnglerIsAnAcceptedParticipant();
        MockCatchRepository.CorrectAnglerAsync(Arg.Any<PersistCatchAnglerArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        GivenTheRefreshedDetailIsReturned(catchId);

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockCatchRepository.Received(1).CorrectAnglerAsync(
            Arg.Is<PersistCatchAnglerArgs>(args =>
                args.CatchId == catchId && args.CaughtByUserId == CorrectedAnglerUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowCorrectingTheAnglerToTheTripOwner()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId);
        MockCurrentUser.UserId.Returns(RecorderUserId);
        MockTripAccessService
            .ResolveForAsync(TripId, TripOwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(TripAccess.Resolve(Trip(), TripOwnerUserId, participant: null)));
        MockCatchRepository.CorrectAnglerAsync(Arg.Any<PersistCatchAnglerArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        GivenTheRefreshedDetailIsReturned(catchId, TripOwnerUserId);

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, TripOwnerUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockCatchRepository.Received(1).CorrectAnglerAsync(
            Arg.Is<PersistCatchAnglerArgs>(args =>
                args.CatchId == catchId && args.CaughtByUserId == TripOwnerUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotTouchThePersistenceLayerWhenTheSelectedAnglerIsUnchanged()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId);
        MockCurrentUser.UserId.Returns(RecorderUserId);
        GivenTheRefreshedDetailIsReturned(catchId, OriginalAnglerUserId);

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, OriginalAnglerUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockCatchRepository.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<PersistCatchAnglerArgs>(),
            Arg.Any<CancellationToken>());
        await MockTripAccessService.DidNotReceive().ResolveForAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotPerformAnyObjectStorageOperationWhenCorrectingTheAngler()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        GivenExistingCatch(catchId);
        MockCurrentUser.UserId.Returns(RecorderUserId);
        MockObjectStorage.IsConfigured.Returns(true);
        GivenTheCorrectedAnglerIsAnAcceptedParticipant();
        MockCatchRepository.CorrectAnglerAsync(Arg.Any<PersistCatchAnglerArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        GivenTheRefreshedDetailIsReturned(catchId);

        // Act
        var result = await Sut.CorrectAnglerAsync(Args(catchId, CorrectedAnglerUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockObjectStorage.DidNotReceive().DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private void GivenTheCorrectedAnglerIsAnAcceptedParticipant()
    {
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
    }

    private void GivenTheRefreshedDetailIsReturned(Guid catchId, Guid? caughtByUserId = null)
    {
        var angler = caughtByUserId ?? CorrectedAnglerUserId;
        MockCatchRepository.GetDetailForUserAsync(catchId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchDetail?>(new CatchDetail
            {
                Catch = new Catch
                {
                    Id = catchId,
                    CaughtByUserId = angler,
                    RecordedByUserId = RecorderUserId,
                    TripId = TripId,
                    CaughtOn = StartedOn
                },
                AnglerName = "Corrected Angler",
                RecordedByName = "Recorder"
            }));
    }

    private static readonly Guid PhotographId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private void GivenExistingCatch(Guid catchId)
    {
        MockCatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(new Catch
            {
                Id = catchId,
                CaughtByUserId = OriginalAnglerUserId,
                RecordedByUserId = RecorderUserId,
                TripId = TripId,
                CaughtOn = StartedOn,
                Photographs =
                [
                    new CatchPhotograph
                    {
                        Id = PhotographId,
                        CatchId = catchId,
                        ContentType = PhotographContentTypeConstants.Jpeg
                    }
                ]
            }));
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

    private static CorrectCatchAnglerArgs Args(Guid catchId, Guid CaughtByUserId)
    {
        return new CorrectCatchAnglerArgs
        {
            CatchId = catchId,
            CaughtByUserId = CaughtByUserId
        };
    }
}
