using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportPersistenceServiceTests;

public class WhenTestingPersistAsync : BaseImportPersistenceServiceTest
{
    [Fact]
    public async Task ItShouldPersistTheExactExplicitlyConfirmedHistoricalOffset()
    {
        // Arrange
        var wallClock = new DateTime(2009, 2, 2, 15, 6, 0, DateTimeKind.Local);
        var confirmed = ImportTimestampModel.FromLocalWallClock(
                wallClock,
                ImportTimestampSourceEnum.ExifOriginal)
            .ConfirmLocalWallClock(wallClock, TimeSpan.FromHours(5.5));
        var batch = Batch(ImportTripDecisionEnum.NoTrip, timestamp: confirmed);
        var sut = CreateSut();

        // Act
        await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        var expected = new DateTimeOffset(2009, 2, 2, 15, 6, 0, TimeSpan.FromHours(5.5));
        await CatchClient.Received(1).UpsertAsync(
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
        await CatchClient.Received(1).UpsertAsync(
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
            Arg.Is<InviteTripParticipantDto>(request => request.UserId == ParticipantId),
            Arg.Any<CancellationToken>());
        await CatchClient.Received(1).UpsertAsync(
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
                && record.Location != null),
            Arg.Any<CancellationToken>());
        await BlobRegistry.Received(1).GetBytesAsync("token", Arg.Any<CancellationToken>());
        await CatchClient.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Is<RecordPhotographDto>(photo => photo.PhotographId == PhotoId && photo.ObjectKey == "object"),
            Arg.Any<CancellationToken>());
        await CatchClient.Received(3).GetAsync(CatchId, Arg.Any<CancellationToken>());
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
            Arg.Any<InviteTripParticipantDto>(),
            Arg.Any<CancellationToken>());
        await CatchClient.Received(1).UpsertAsync(
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
            Arg.Any<InviteTripParticipantDto>(),
            Arg.Any<CancellationToken>());
        await CatchClient.Received(1).UpsertAsync(
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
            .Returns(Task.FromException<TripDto?>(new HttpRequestException("failed")));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Trip);
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().CreatePhotographUploadAsync(
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
        ParticipantClient.InviteAsync(
                TripId,
                Arg.Any<InviteTripParticipantDto>(),
                Arg.Any<CancellationToken>())
            .Returns((TripParticipantsDto?)null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSurfaceUploadFailureWithoutRecordingThePhotograph()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchClient.UploadPhotographAsync(
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("upload failed")));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Photograph);
        await CatchClient.DidNotReceive().RecordPhotographAsync(
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
        TripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(MatchingTrip());

        // Act
        await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        await TripClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        await CatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record => record.TripId == TripId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWithoutOverwritingAConflictingAuthoritativeTrip()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(
            new TripDetailDto(new TripViewDto(TripId, UserId, "Completed", CaughtOn.AddHours(1))));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await TripClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReuseAMatchingCatchAndRegisteredPhotograph()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(MatchingCatch(includePhotograph: true));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.CatchIds.Should().Equal(CatchId);
        result.PhotographCount.Should().Be(1);
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(), Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>());
        await BlobRegistry.DidNotReceive().GetBytesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWithoutOverwritingAConflictingAuthoritativeCatch()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(
            MatchingCatch(includePhotograph: false) with { SpeciesName = "Pike" });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenARegisteredPhotographIdentityHasDifferentContent()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(
            MatchingCatch(includePhotograph: true) with
            {
                Photographs = [new CatchPhotographViewDto(PhotoId, "image/png", "https://photo.test")]
            });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotInviteAnExistingParticipantAgain()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();
        ParticipantClient.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(
            new TripParticipantsDto(TripId, "Owner")
            {
                Participants = [new TripParticipantDto(ParticipantId, "Accepted", "Angler", null, CaughtOn)]
            });

        // Act
        await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        await ParticipantClient.DidNotReceive().InviteAsync(
            Arg.Any<Guid>(), Arg.Any<InviteTripParticipantDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReportSuccessWhenTheAuthoritativeCatchCannotBeReread()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns((CatchViewDto?)null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
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
        CatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            authoritativeCatch = call.Arg<CatchDto>();
            return authoritativeCatch;
        });
        CatchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(_ =>
            authoritativeCatch is null ? null : MatchingCatch(photographRegistered));
        CatchClient.UploadPhotographAsync(
                Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                uploadAttempts++;
                return uploadAttempts == 1
                    ? Task.FromException(new HttpRequestException("interrupted"))
                    : Task.CompletedTask;
            });
        CatchClient.RecordPhotographAsync(
                CatchId, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                photographRegistered = true;
                return Task.CompletedTask;
            });

        // Act
        var firstAttempt = await sut.PersistAsync(batch, CancellationToken.None);
        firstAttempt.IsSuccess.Should().BeFalse();
        firstAttempt.Failure.Should().Be(ImportPersistenceFailureEnum.Photograph);
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.CatchIds.Should().Equal(CatchId);
        await CatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(record => record.Id == CatchId),
            Arg.Any<CancellationToken>());
        await CatchClient.Received(2).UploadPhotographAsync(
            "https://upload.test",
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { 1, 2, 3 })),
            "image/jpeg",
            Arg.Any<CancellationToken>());
        await CatchClient.Received(1).RecordPhotographAsync(
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

    private static TripDetailDto MatchingTrip()
    {
        return new TripDetailDto(new TripViewDto(TripId, UserId, "Completed", CaughtOn, CaughtOn));
    }

    private static CatchViewDto MatchingCatch(bool includePhotograph)
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
            Photographs = includePhotograph
                ? [new CatchPhotographViewDto(PhotoId, "image/jpeg", "https://photo.test")]
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
