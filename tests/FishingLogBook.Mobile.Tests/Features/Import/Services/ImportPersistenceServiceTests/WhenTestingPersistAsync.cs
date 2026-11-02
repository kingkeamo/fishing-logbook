using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.Services.ImportPersistenceServiceTests;

public class WhenTestingPersistAsync : BaseImportPersistenceServiceTest
{
    [Fact]
    public async Task ItShouldPersistTheExactExplicitlyConfirmedHistoricalOffset()
    {
        // Arrange
        var wallClock = new DateTime(2009, 2, 2, 15, 6, 0, DateTimeKind.Local);
        var confirmed = ImportTimestampModel.UserConfirmed(
            new DateTimeOffset(DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified), TimeSpan.FromHours(5.5)));
        var batch = Batch(ImportTripDecisionEnum.NoTrip, timestamp: confirmed);
        var sut = CreateSut();

        // Act
        await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        var expected = new DateTimeOffset(2009, 2, 2, 15, 6, 0, TimeSpan.FromHours(5.5));
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record => record.CaughtOn == expected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistAnExplicitExifOffsetUnchanged()
    {
        // Arrange
        var explicitTimestamp = ImportTimestampModel.FromExplicitInstant(
            CaughtOn,
            ImportTimestampSourceEnum.ExifOriginal);
        var batch = Batch(ImportTripDecisionEnum.NoTrip, timestamp: explicitTimestamp);
        var sut = CreateSut();

        // Act
        await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record => record.CaughtOn == CaughtOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistANewTripParticipantsCatchAndPhotographThroughAuthoritativeClients()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.CreatedTripIds.Should().Equal(TripId);
        result.CatchIds.Should().Equal(CatchId);
        result.PhotographCount.Should().Be(1);
        await TripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip =>
                trip.Id == TripId
                && trip.OwnerUserId == UserId
                && trip.Status == "Completed"
                && trip.StartedOn == CaughtOn
                && trip.EndedOn == CaughtOn),
            Arg.Any<CancellationToken>());
        await ParticipantClient.Received(1).InviteAsync(
            TripId,
            ParticipantId,
            Arg.Any<CancellationToken>());
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record =>
                record.Id == CatchId
                && record.TripId == TripId
                && record.CaughtOn == CaughtOn
                && record.CaughtByUserId == UserId
                && record.RecordedByUserId == UserId
                && record.Method == "Fly"
                && record.SpeciesName == "Brown Trout"
                && record.Weight == 2.5m
                && record.Length == 42m
                && record.PlaceName == "River Corrib, Galway, Ireland"
                && record.Location != null),
            Arg.Any<CancellationToken>());
        await BlobRegistry.Received(1).GetBytesAsync("token", Arg.Any<CancellationToken>());
        await Uploads.Received(1).UploadAsync(
            "https://upload.test",
            Arg.Any<Stream>(),
            "image/jpeg",
            Arg.Any<CancellationToken>());
        await CatchSync.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(photo => photo.PhotographId == PhotoId && photo.ObjectKey == "object"),
            Arg.Any<CancellationToken>());
        await CatchRemote.Received(3).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistTheReviewedPlaceNameRatherThanProviderMetadata()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var proposal = batch.CatchProposals.Single();
        proposal.SetLocation(proposal.Location!.WithPlaceName("Galway, Ireland"));
        proposal.MarkReviewed();
        var sut = CreateSut();

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record =>
                record.PlaceName == "Galway, Ireland"
                && record.Location != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseAnExistingTripWithoutMutatingParticipants()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.UseExisting);
        var sut = CreateSut();

        // Act
        await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        await TripClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        await ParticipantClient.DidNotReceive().InviteAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record => record.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistAStandaloneCatchWithoutATrip()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();

        // Act
        await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        await TripClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        await ParticipantClient.DidNotReceive().InviteAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record => record.TripId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopBeforeCreatingACatchWhenTripCreationFails()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Trip);
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopBeforeCreatingACatchWhenParticipantPersistenceFails()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();
        ParticipantClient.InviteAsync(TripId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripParticipantsDto>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Trip);
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSurfaceUploadFailureWithoutRecordingThePhotograph()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        Uploads.UploadAsync(
                Arg.Any<string>(),
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(PhotographUploadOutcomeEnum.Unavailable);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Photograph);
        await CatchSync.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReuseAMatchingAuthoritativeTripWithoutUpsertingIt()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<TripDetailDto>.Succeeded(MatchingTrip()));

        // Act
        await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        await TripClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record => record.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWithoutOverwritingAConflictingAuthoritativeTrip()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<TripDetailDto>.Succeeded(
                new TripDetailDto(new TripViewDto(TripId, UserId, "Completed", CaughtOn.AddHours(1)))));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await TripClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReuseAMatchingCatchAndRegisteredPhotograph()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<CatchViewDto>.Succeeded(MatchingCatch(includePhotograph: true)));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.CatchIds.Should().Equal(CatchId);
        result.PhotographCount.Should().Be(1);
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
        await BlobRegistry.DidNotReceive().GetBytesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCompleteAPlaceholderPhotographOnRetry()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        var completed = false;
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(_ =>
            ApiResultModel<CatchViewDto>.Succeeded(
                MatchingCatch(includePhotograph: true, photographCompleted: completed)));
        CatchSync.RecordPhotographAsync(CatchId, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                completed = true;
                return ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance);
            });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await BlobRegistry.Received(1).GetBytesAsync("token", Arg.Any<CancellationToken>());
        await CatchSync.Received(1).CreatePhotographUploadAsync(
            CatchId,
            Arg.Is<PhotographUploadRequestDto>(request => request.PhotographId == PhotoId),
            Arg.Any<CancellationToken>());
        await Uploads.Received(1).UploadAsync(
            "https://upload.test",
            Arg.Any<Stream>(),
            "image/jpeg",
            Arg.Any<CancellationToken>());
        await CatchSync.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(photo => photo.PhotographId == PhotoId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotTreatPlaceholderMetadataAsFinalSuccess()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<CatchViewDto>.Succeeded(
                MatchingCatch(includePhotograph: true, photographCompleted: false)));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await Uploads.Received(1).UploadAsync(
            "https://upload.test",
            Arg.Any<Stream>(),
            "image/jpeg",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSkipOnlyCompletedPhotographsDuringAPartialMultiPhotoRetry()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip, includeSecondPhoto: true);
        var sut = CreateSut();
        var secondCompleted = false;
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(_ =>
            ApiResultModel<CatchViewDto>.Succeeded(MatchingCatch(includePhotograph: false) with
            {
                Photographs =
                [
                    new CatchPhotographViewDto(PhotoId, "image/jpeg", "https://photo.test/one"),
                    new CatchPhotographViewDto(
                        SecondPhotoId,
                        "image/png",
                        secondCompleted ? "https://photo.test/two" : null)
                ]
            }));
        CatchSync.RecordPhotographAsync(CatchId, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                secondCompleted = call.Arg<RecordPhotographDto>().PhotographId == SecondPhotoId;
                return ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance);
            });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await CatchSync.Received(1).CreatePhotographUploadAsync(
            CatchId,
            Arg.Is<PhotographUploadRequestDto>(request => request.PhotographId == SecondPhotoId),
            Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().CreatePhotographUploadAsync(
            CatchId,
            Arg.Is<PhotographUploadRequestDto>(request => request.PhotographId == PhotoId),
            Arg.Any<CancellationToken>());
        await CatchSync.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(photo => photo.PhotographId == SecondPhotoId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWithoutOverwritingAConflictingAuthoritativeCatch()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<CatchViewDto>.Succeeded(
                MatchingCatch(includePhotograph: false) with { SpeciesName = "Pike" }));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenARegisteredPhotographIdentityHasDifferentContent()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<CatchViewDto>.Succeeded(MatchingCatch(includePhotograph: true) with
            {
                Photographs = [new CatchPhotographViewDto(PhotoId, "image/png", "https://photo.test")]
            }));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchSync.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotInviteAnExistingParticipantAgain()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();
        ParticipantClient.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<TripParticipantsDto>.Succeeded(new TripParticipantsDto(TripId, "Owner")
            {
                Participants = [new TripParticipantDto(ParticipantId, "Accepted", "Angler", null, CaughtOn)]
            }));

        // Act
        await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        await ParticipantClient.DidNotReceive().InviteAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReportSuccessWhenTheAuthoritativeCatchCannotBeReread()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<CatchViewDto>.Failed(ApiOutcomeEnum.NotFound));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchSync.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReconcilePartialPersistenceBeforeRetryingThePhotograph()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchDto? authoritativeCatch = null;
        var photographRegistered = false;
        var uploadAttempts = 0;
        CatchSync.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            authoritativeCatch = call.Arg<CatchDto>();
            return ApiResultModel<CatchDto>.Succeeded(authoritativeCatch);
        });
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(_ =>
            authoritativeCatch is null
                ? ApiResultModel<CatchViewDto>.Failed(ApiOutcomeEnum.NotFound)
                : ApiResultModel<CatchViewDto>.Succeeded(MatchingCatch(photographRegistered)));
        Uploads.UploadAsync(
                Arg.Any<string>(),
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                uploadAttempts++;
                return uploadAttempts == 1
                    ? PhotographUploadOutcomeEnum.Unavailable
                    : PhotographUploadOutcomeEnum.Uploaded;
            });
        CatchSync.RecordPhotographAsync(CatchId, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                photographRegistered = true;
                return ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance);
            });

        // Act
        var firstAttempt = await sut.PersistAsync(batch, CancellationToken.None);
        firstAttempt.IsSuccess.Should().BeFalse();
        firstAttempt.Failure.Should().Be(ImportPersistenceFailureEnum.Photograph);
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.CatchIds.Should().Equal(CatchId);
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record => record.Id == CatchId),
            Arg.Any<CancellationToken>());
        await Uploads.Received(2).UploadAsync(
            "https://upload.test",
            Arg.Any<Stream>(),
            "image/jpeg",
            Arg.Any<CancellationToken>());
        await CatchSync.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(photo => photo.PhotographId == PhotoId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportMeaningfulPersistenceProgress()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        var progress = new RecordingProgress();

        // Act
        await sut.PersistAsync(batch, CancellationToken.None, progress);

        // Assert
        progress.Values.Select(value => value.Stage).Should().Equal(
            ImportPersistenceStageEnum.SavingTrip,
            ImportPersistenceStageEnum.SavingCatch,
            ImportPersistenceStageEnum.UploadingPhotograph,
            ImportPersistenceStageEnum.Verifying);
        progress.Values.Should().OnlyContain(value => value.Current == 1 && value.Total == 1);
    }

    [Fact]
    public async Task ItShouldAbortWhenPersistenceIsCancelled()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var act = () => sut.PersistAsync(batch, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopAfterUploadWhenPhotographRegistrationFails()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchSync.RecordPhotographAsync(CatchId, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<NoContentModel>.Failed(ApiOutcomeEnum.Unavailable));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Photograph);
        await Uploads.Received(1).UploadAsync(
            "https://upload.test",
            Arg.Any<Stream>(),
            "image/jpeg",
            Arg.Any<CancellationToken>());
        await CatchSync.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(photo => photo.PhotographId == PhotoId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryPhotographRegistrationWithoutCreatingAnotherCatch()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        var registered = false;
        var recordAttempts = 0;
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(_ =>
            ApiResultModel<CatchViewDto>.Succeeded(
                MatchingCatch(includePhotograph: true, photographCompleted: registered)));
        CatchSync.RecordPhotographAsync(CatchId, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                recordAttempts++;
                if (recordAttempts == 1)
                {
                    return ApiResultModel<NoContentModel>.Failed(ApiOutcomeEnum.Unavailable);
                }

                registered = true;
                return ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance);
            });

        // Act
        var first = await sut.PersistAsync(batch, CancellationToken.None);
        first.IsSuccess.Should().BeFalse();
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.CatchIds.Should().Equal(CatchId);
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await CatchSync.Received(2).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(photo => photo.PhotographId == PhotoId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheFirstCatchWhenALaterCatchFailsAndRetryOnlyTheFailure()
    {
        // Arrange
        var secondCatchId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var batch = TwoCatchBatch(secondCatchId);
        var sut = CreateSut();
        var secondAttempts = 0;
        CatchDto? firstCatch = null;
        CatchDto? secondCatch = null;
        CatchSync.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var dto = call.Arg<CatchDto>();
            if (dto.Id == CatchId)
            {
                firstCatch = dto;
                return ApiResultModel<CatchDto>.Succeeded(dto);
            }

            secondAttempts++;
            if (secondAttempts == 1)
            {
                return ApiResultModel<CatchDto>.Failed(ApiOutcomeEnum.Unavailable);
            }

            secondCatch = dto;
            return ApiResultModel<CatchDto>.Succeeded(dto);
        });
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(_ =>
            firstCatch is null
                ? ApiResultModel<CatchViewDto>.Failed(ApiOutcomeEnum.NotFound)
                : ApiResultModel<CatchViewDto>.Succeeded(MatchingCatch(includePhotograph: true)));
        CatchRemote.GetAsync(secondCatchId, Arg.Any<CancellationToken>()).Returns(_ =>
            secondCatch is null
                ? ApiResultModel<CatchViewDto>.Failed(ApiOutcomeEnum.NotFound)
                : ApiResultModel<CatchViewDto>.Succeeded(MatchingCatch(includePhotograph: true) with
                {
                    Id = secondCatchId,
                    Weight = 1.1m,
                    Length = 30m,
                    Photographs = [new CatchPhotographViewDto(SecondPhotoId, "image/png", "https://photo.test")]
                }));
        CatchSync.CreatePhotographUploadAsync(secondCatchId, Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<PhotographUploadDto>.Succeeded(
                new PhotographUploadDto("object-2", "https://upload.test")));
        CatchSync.RecordPhotographAsync(secondCatchId, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance));

        // Act
        var first = await sut.PersistAsync(batch, CancellationToken.None);
        first.IsSuccess.Should().BeFalse();
        first.Failure.Should().Be(ImportPersistenceFailureEnum.Catch);
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.CatchIds.Should().Equal(CatchId, secondCatchId);
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record => record.Id == CatchId),
            Arg.Any<CancellationToken>());
        await CatchSync.Received(2).UpsertAsync(
            Arg.Is<CatchDto>(record => record.Id == secondCatchId),
            Arg.Any<CancellationToken>());
    }

    private static ImportBatchModel TwoCatchBatch(Guid secondCatchId)
    {
        var batch = Batch(ImportTripDecisionEnum.NoTrip, includeSecondPhoto: true);
        var first = batch.CatchProposals.Single();
        first.SetPhotos([PhotoId]);
        first.MarkReviewed();
        var second = new ImportCatchProposalModel(
            secondCatchId,
            [SecondPhotoId],
            ImportTimestampModel.UserConfirmed(CaughtOn),
            first.Method,
            first.Species,
            first.Location,
            weight: 1.1m,
            length: 30m);
        second.MarkReviewed();
        batch.AddCatchProposal(second);
        return batch;
    }

    private static TripDetailDto MatchingTrip()
    {
        return new TripDetailDto(new TripViewDto(TripId, UserId, "Completed", CaughtOn, CaughtOn));
    }

    private static CatchViewDto MatchingCatch(bool includePhotograph, bool photographCompleted = true)
    {
        return new CatchViewDto(CatchId, UserId, CaughtOn, new CatchLocationExposureDto
        {
            Latitude = 53.1,
            Longitude = -6.2,
            CapturedOn = CaughtOn,
            Source = LocationDefaults.PhotoMetadata,
            Visibility = LocationDefaults.Private
        })
        {
            RecordedByUserId = UserId,
            SpeciesName = "Brown Trout",
            Method = "Fly",
            Weight = 2.5m,
            Length = 42m,
            PlaceName = "River Corrib, Galway, Ireland",
            Photographs = includePhotograph
                ? [new CatchPhotographViewDto(
                    PhotoId,
                    "image/jpeg",
                    photographCompleted ? "https://photo.test" : null)]
                : []
        };
    }

    private sealed class RecordingProgress : IProgress<ImportPersistenceProgressModel>
    {
        public List<ImportPersistenceProgressModel> Values { get; } = [];

        public void Report(ImportPersistenceProgressModel value)
        {
            Values.Add(value);
        }
    }
}
