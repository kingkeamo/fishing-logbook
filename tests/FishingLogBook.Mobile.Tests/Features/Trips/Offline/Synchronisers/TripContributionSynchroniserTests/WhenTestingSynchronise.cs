using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Clients;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Synchronisers;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;
using FishingLogBook.Shared.Dtos;
using Microsoft.Data.Sqlite;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Offline.Synchronisers.TripContributionSynchroniserTests;

public sealed class WhenTestingSynchronise : BaseTripServiceTest
{
    private readonly ITripDependencyService _dependency = Substitute.For<ITripDependencyService>();
    private readonly IPhotographFileStore _files = Substitute.For<IPhotographFileStore>();
    private readonly IFileSystemAdapter _fileSystem = Substitute.For<IFileSystemAdapter>();
    private readonly IPhotographUploadClient _uploader = Substitute.For<IPhotographUploadClient>();

    private TripContributionSynchroniser Note => Create(OutboxOperationTypeEnum.WriteTripNote);

    private TripContributionSynchroniser Photograph => Create(OutboxOperationTypeEnum.WriteTripPhotograph);

    private TripContributionSynchroniser Create(OutboxOperationTypeEnum type)
    {
        return new TripContributionSynchroniser(type, TripStore, MockClient, _dependency, _files, _fileSystem, _uploader);
    }

    private async Task<LocalTripRecordModel> StartTripAsync()
    {
        MockConnectivity.IsOnline.Returns(false);
        var trip = await TripSut.StartAsync("Corrib", null, CancellationToken.None);
        MockClient.ClearReceivedCalls();
        return trip;
    }

    private async Task<OutboxOperationModel> NextOperationAsync(OutboxOperationTypeEnum type)
    {
        return (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None)).Single(operation => operation.OperationType == type);
    }

    private async Task<(Guid NoteId, OutboxOperationModel Operation)> SaveNoteAsync(LocalTripRecordModel trip, bool deleted = false)
    {
        var note = new TripNoteDto(Guid.NewGuid(), trip.Detail.Trip.Id, "Water dropped", Now) { CreatedByUserId = OwnerUserId };
        await TripSut.SaveNoteAsync(trip, note, deleted, CancellationToken.None);
        return (note.Id, await NextOperationAsync(OutboxOperationTypeEnum.WriteTripNote));
    }

    private async Task<(Guid PhotoId, OutboxOperationModel Operation)> SavePhotographAsync(LocalTripRecordModel trip)
    {
        var photo = new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Now) { ContributedByUserId = OwnerUserId };
        await TripSut.SavePhotographAsync(trip, photo, "photos/trip.jpg", deleted: false, CancellationToken.None);
        return (photo.Id, await NextOperationAsync(OutboxOperationTypeEnum.WriteTripPhotograph));
    }

    private async Task CompleteAsync(OutboxOperationResultModel result)
    {
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        await result.Persist!(connection, transaction, CancellationToken.None);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task ItShouldFailTerminallyWhenTheOperationPayloadIsInvalid()
    {
        // Arrange
        var operation = (await StartTripAsync().ContinueWith(_ => new OutboxOperationModel
        {
            Id = Guid.NewGuid(),
            UserId = OwnerUserId,
            AggregateId = Guid.NewGuid(),
            AggregateType = OutboxAggregateTypeEnum.TripNote,
            OperationType = OutboxOperationTypeEnum.WriteTripNote,
            PayloadJson = JsonSerializer.Serialize(new TripOperationPayloadModel(Guid.Empty)),
            Status = OutboxStatusEnum.Pending,
            AttemptCount = 0,
            CreatedOn = Now,
            AvailableAt = Now
        }));
        // Act
        var result = await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Terminal);
        result.ErrorCode.Should().Be("InvalidTripOperation");
    }

    [Fact]
    public async Task ItShouldHoldAContributionBackUntilItsTripIsReady()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (_, operation) = await SaveNoteAsync(trip);
        _dependency.CheckAsync(OwnerUserId, trip.Detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(OutboxOperationResultModel.Retry("WaitingForTrip"));
        // Act
        var result = await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
        result.ErrorCode.Should().Be("WaitingForTrip");
        await MockClient.DidNotReceive().RecordNoteAsync(Arg.Any<Guid>(), Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAnAlreadyDeletedNoteAsDeletedWhenTheServerNoLongerHasIt()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (noteId, operation) = await SaveNoteAsync(trip, deleted: true);
        MockClient.DeleteNoteAsync(trip.Detail.Trip.Id, noteId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<NoContentModel>.Failed(ApiOutcomeEnum.NotFound));
        // Act
        var result = await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Completed);
        await MockClient.Received(1).DeleteNoteAsync(trip.Detail.Trip.Id, noteId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryWhenTheServerReturnsADifferentNote()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (_, operation) = await SaveNoteAsync(trip);
        MockClient.RecordNoteAsync(trip.Detail.Trip.Id, Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>()).Returns(
            ApiResultModel<TripNoteDto>.Succeeded(new TripNoteDto(Guid.NewGuid(), trip.Detail.Trip.Id, "x", Now) { CreatedByUserId = OwnerUserId }));
        // Act
        var result = await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
        result.ErrorCode.Should().Be("InvalidTripResponse");
    }

    [Fact]
    public async Task ItShouldPostTheNoteWithItsClientIdAndMarkOnlyThatContributionSynchronised()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (noteId, operation) = await SaveNoteAsync(trip);
        MockClient.RecordNoteAsync(trip.Detail.Trip.Id, Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>()).Returns(call =>
            ApiResultModel<TripNoteDto>.Succeeded(new TripNoteDto(call.Arg<RecordTripNoteDto>().NoteId, trip.Detail.Trip.Id, call.Arg<RecordTripNoteDto>().Text, Now)
            { CreatedByUserId = OwnerUserId }));
        // Act
        var result = await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        await CompleteAsync(result);
        // Assert
        await MockClient.Received(1).RecordNoteAsync(
            trip.Detail.Trip.Id,
            Arg.Is<RecordTripNoteDto>(sent => sent.NoteId == noteId && sent.Text == "Water dropped" && sent.RecordedOn == Now),
            Arg.Any<CancellationToken>());
        (await TripStore.ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None))
            .Single().IsPending.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldFailTerminallyWhenThePhotographFileIsMissingAndNeverCallTheServer()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (_, operation) = await SavePhotographAsync(trip);
        _files.TryResolveRelativePath(OwnerUserId, "photos/trip.jpg", out Arg.Any<string>()!).Returns(call =>
        {
            call[2] = "/files/trip.jpg";
            return true;
        });
        _fileSystem.FileExists("/files/trip.jpg").Returns(false);
        // Act
        var result = await Photograph.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Terminal);
        result.ErrorCode.Should().Be("PhotographFileMissing");
        await MockClient.DidNotReceive().CreatePhotographUploadAsync(Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRegisterAPhotographWhoseBytesFailedToUpload()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (photoId, operation) = await SavePhotographAsync(trip);
        ArrangeFile();
        MockClient.CreatePhotographUploadAsync(trip.Detail.Trip.Id, Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<PhotographUploadDto>.Succeeded(new PhotographUploadDto("trips/key", "https://upload.example/put")));
        _uploader.UploadAsync("https://upload.example/put", Arg.Any<Stream>(), "image/jpeg", Arg.Any<CancellationToken>()).Returns(PhotographUploadOutcomeEnum.Unavailable);
        // Act
        var result = await Photograph.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
        result.ErrorCode.Should().Be("PhotographUploadFailed");
        await MockClient.DidNotReceive().RecordPhotographAsync(Arg.Any<Guid>(), Arg.Any<RecordTripPhotographDto>(), Arg.Any<CancellationToken>());
        (await TripStore.ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None))
            .Single(item => item.Id == photoId).IsPending.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRequestAnUrlThenUploadTheBytesThenRegisterThePhotographWithTheSameIdentity()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (photoId, operation) = await SavePhotographAsync(trip);
        ArrangeFile();
        MockClient.CreatePhotographUploadAsync(trip.Detail.Trip.Id, Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<PhotographUploadDto>.Succeeded(new PhotographUploadDto("trips/key", "https://upload.example/put")));
        _uploader.UploadAsync("https://upload.example/put", Arg.Any<Stream>(), "image/jpeg", Arg.Any<CancellationToken>()).Returns(PhotographUploadOutcomeEnum.Uploaded);
        MockClient.RecordPhotographAsync(trip.Detail.Trip.Id, Arg.Any<RecordTripPhotographDto>(), Arg.Any<CancellationToken>()).Returns(call =>
            ApiResultModel<TripPhotographDto>.Succeeded(new TripPhotographDto(photoId, trip.Detail.Trip.Id, "trips/key", "image/jpeg", Now) { ContributedByUserId = OwnerUserId }));
        // Act
        var result = await Photograph.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        await CompleteAsync(result);
        // Assert
        Received.InOrder(() =>
        {
            MockClient.CreatePhotographUploadAsync(trip.Detail.Trip.Id, Arg.Is<PhotographUploadRequestDto>(request => request.PhotographId == photoId && request.ContentType == "image/jpeg"), Arg.Any<CancellationToken>());
            _uploader.UploadAsync("https://upload.example/put", Arg.Any<Stream>(), "image/jpeg", Arg.Any<CancellationToken>());
            MockClient.RecordPhotographAsync(trip.Detail.Trip.Id, Arg.Is<RecordTripPhotographDto>(record => record.PhotographId == photoId && record.ObjectKey == "trips/key" && record.ContentType == "image/jpeg"), Arg.Any<CancellationToken>());
        });
        (await TripStore.ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None))
            .Single(item => item.Id == photoId).IsPending.Should().BeFalse();
    }

    [Fact]
    public void ItShouldReportTheOperationTypeItWasCreatedFor()
    {
        // Arrange
        // Act
        var note = Note.OperationType;
        var photograph = Photograph.OperationType;
        // Assert
        note.Should().Be(OutboxOperationTypeEnum.WriteTripNote);
        photograph.Should().Be(OutboxOperationTypeEnum.WriteTripPhotograph);
    }

    [Fact]
    public async Task ItShouldFailTerminallyWhenTheOperationPayloadIsEmptyJson()
    {
        // Arrange
        await StartTripAsync();
        var operation = new OutboxOperationModel
        {
            Id = Guid.NewGuid(),
            UserId = OwnerUserId,
            AggregateId = Guid.NewGuid(),
            AggregateType = OutboxAggregateTypeEnum.TripNote,
            OperationType = OutboxOperationTypeEnum.WriteTripNote,
            PayloadJson = "null",
            Status = OutboxStatusEnum.Pending,
            AttemptCount = 0,
            CreatedOn = Now,
            AvailableAt = Now
        };
        // Act
        var result = await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.ErrorCode.Should().Be("InvalidTripOperation");
    }

    [Fact]
    public async Task ItShouldCompleteWithoutCallingTheServerWhenTheContributionNoLongerExists()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (noteId, operation) = await SaveNoteAsync(trip);
        var missing = new OutboxOperationModel
        {
            Id = operation.Id,
            UserId = OwnerUserId,
            AggregateId = Guid.NewGuid(),
            AggregateType = operation.AggregateType,
            OperationType = operation.OperationType,
            PayloadJson = operation.PayloadJson,
            Status = operation.Status,
            AttemptCount = 0,
            CreatedOn = Now,
            AvailableAt = Now
        };
        // Act
        var result = await Note.SynchroniseAsync(missing, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Completed);
        noteId.Should().NotBe(missing.AggregateId);
        await MockClient.DidNotReceive().RecordNoteAsync(Arg.Any<Guid>(), Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCompleteWithoutCallingTheServerWhenTheContributionIsAlreadySynchronised()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (_, operation) = await SaveNoteAsync(trip);
        MockClient.RecordNoteAsync(trip.Detail.Trip.Id, Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>()).Returns(call =>
            ApiResultModel<TripNoteDto>.Succeeded(new TripNoteDto(call.Arg<RecordTripNoteDto>().NoteId, trip.Detail.Trip.Id, "x", Now) { CreatedByUserId = OwnerUserId }));
        await CompleteAsync(await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None));
        MockClient.ClearReceivedCalls();
        // Act
        var result = await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Completed);
        await MockClient.DidNotReceive().RecordNoteAsync(Arg.Any<Guid>(), Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailTerminallyWhenTheTripNoLongerExistsOnTheServerForANote()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (_, operation) = await SaveNoteAsync(trip);
        MockClient.RecordNoteAsync(trip.Detail.Trip.Id, Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripNoteDto>.Failed(ApiOutcomeEnum.NotFound));
        // Act
        var result = await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Terminal);
        result.ErrorCode.Should().Be("TripUnavailable");
    }

    [Fact]
    public async Task ItShouldRetryANoteTheServerCouldNotAcceptYet()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (_, operation) = await SaveNoteAsync(trip);
        MockClient.RecordNoteAsync(trip.Detail.Trip.Id, Arg.Any<RecordTripNoteDto>(), Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripNoteDto>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        var result = await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
    }

    [Fact]
    public async Task ItShouldRetryADeletionTheServerCouldNotAcceptYet()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (noteId, operation) = await SaveNoteAsync(trip, deleted: true);
        MockClient.DeleteNoteAsync(trip.Detail.Trip.Id, noteId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<NoContentModel>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        var result = await Note.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
    }

    [Fact]
    public async Task ItShouldDeleteAPhotographOnTheServerAndMarkItSynchronised()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (photoId, _) = await SavePhotographAsync(trip);
        var photo = new TripPhotographViewDto(photoId, "image/jpeg", Now) { ContributedByUserId = OwnerUserId };
        await TripSut.SavePhotographAsync(trip, photo, null, deleted: true, CancellationToken.None);
        var operation = (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None)).Single(item => item.AggregateId == photoId);
        MockClient.DeletePhotographAsync(trip.Detail.Trip.Id, photoId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance));
        // Act
        var result = await Photograph.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        await CompleteAsync(result);
        // Assert
        await MockClient.Received(1).DeletePhotographAsync(trip.Detail.Trip.Id, photoId, Arg.Any<CancellationToken>());
        (await TripStore.ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None)).Single(item => item.Id == photoId).IsPending.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldReportTheOutcomeWhenTheUploadUrlCannotBeCreated()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (_, operation) = await SavePhotographAsync(trip);
        ArrangeFile();
        MockClient.CreatePhotographUploadAsync(trip.Detail.Trip.Id, Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<PhotographUploadDto>.Failed(ApiOutcomeEnum.NotFound));
        // Act
        var result = await Photograph.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Terminal);
        result.ErrorCode.Should().Be("TripUnavailable");
        await _uploader.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportTheOutcomeWhenTheUploadedPhotographCannotBeRegistered()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (_, operation) = await SavePhotographAsync(trip);
        ArrangeFile();
        MockClient.CreatePhotographUploadAsync(trip.Detail.Trip.Id, Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<PhotographUploadDto>.Succeeded(new PhotographUploadDto("trips/key", "https://upload.example/put")));
        _uploader.UploadAsync("https://upload.example/put", Arg.Any<Stream>(), "image/jpeg", Arg.Any<CancellationToken>()).Returns(PhotographUploadOutcomeEnum.Uploaded);
        MockClient.RecordPhotographAsync(trip.Detail.Trip.Id, Arg.Any<RecordTripPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripPhotographDto>.Failed(ApiOutcomeEnum.Unavailable));
        // Act
        var result = await Photograph.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
    }

    [Fact]
    public async Task ItShouldRetryWhenTheServerRegistersADifferentPhotograph()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (_, operation) = await SavePhotographAsync(trip);
        ArrangeFile();
        MockClient.CreatePhotographUploadAsync(trip.Detail.Trip.Id, Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<PhotographUploadDto>.Succeeded(new PhotographUploadDto("trips/key", "https://upload.example/put")));
        _uploader.UploadAsync("https://upload.example/put", Arg.Any<Stream>(), "image/jpeg", Arg.Any<CancellationToken>()).Returns(PhotographUploadOutcomeEnum.Uploaded);
        MockClient.RecordPhotographAsync(trip.Detail.Trip.Id, Arg.Any<RecordTripPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripPhotographDto>.Succeeded(new TripPhotographDto(Guid.NewGuid(), trip.Detail.Trip.Id, "trips/key", "image/jpeg", Now) { ContributedByUserId = OwnerUserId }));
        // Act
        var result = await Photograph.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Retry);
        result.ErrorCode.Should().Be("InvalidTripResponse");
    }

    [Fact]
    public async Task ItShouldFailTerminallyWhenAPhotographContributionHasNoPhotographDetails()
    {
        // Arrange
        var trip = await StartTripAsync();
        var id = Guid.NewGuid();
        await TripStore.SaveContributionAsync(OwnerLease, new LocalTripContributionModel { Id = id, TripId = trip.Detail.Trip.Id, UserId = OwnerUserId, RelativeFilePath = "photos/trip.jpg" }, 0, CancellationToken.None);
        var operation = await NextOperationAsync(OutboxOperationTypeEnum.WriteTripPhotograph);
        // Act
        var result = await Photograph.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(OutboxOperationOutcomeEnum.Terminal);
        result.ErrorCode.Should().Be("PhotographFileMissing");
        await MockClient.DidNotReceive().CreatePhotographUploadAsync(Arg.Any<Guid>(), Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailTerminallyWhenAPhotographContributionHasNoFilePath()
    {
        // Arrange
        var trip = await StartTripAsync();
        var photo = new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Now) { ContributedByUserId = OwnerUserId };
        await TripStore.SaveContributionAsync(OwnerLease, new LocalTripContributionModel { Id = photo.Id, TripId = trip.Detail.Trip.Id, UserId = OwnerUserId, Photograph = photo }, 0, CancellationToken.None);
        var operation = await NextOperationAsync(OutboxOperationTypeEnum.WriteTripPhotograph);
        // Act
        var result = await Photograph.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.ErrorCode.Should().Be("PhotographFileMissing");
    }

    [Fact]
    public async Task ItShouldFailTerminallyWhenThePhotographFilePathCannotBeResolvedForThisAccount()
    {
        // Arrange
        var trip = await StartTripAsync();
        var (_, operation) = await SavePhotographAsync(trip);
        _files.TryResolveRelativePath(OwnerUserId, "photos/trip.jpg", out Arg.Any<string>()!).Returns(call =>
        {
            call[2] = string.Empty;
            return false;
        });
        // Act
        var result = await Photograph.SynchroniseAsync(operation, OwnerLease, CancellationToken.None);
        // Assert
        result.ErrorCode.Should().Be("PhotographFileMissing");
        _fileSystem.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    private void ArrangeFile()
    {
        _files.TryResolveRelativePath(OwnerUserId, "photos/trip.jpg", out Arg.Any<string>()!).Returns(call =>
        {
            call[2] = "/files/trip.jpg";
            return true;
        });
        _fileSystem.FileExists("/files/trip.jpg").Returns(true);
        _fileSystem.OpenRead("/files/trip.jpg").Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes("bytes")));
    }
}
