using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportPersistenceServiceTests;

public class WhenTestingPersistAsync : BaseImportPersistenceServiceTest
{
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
        await CatchClient.Received(2).GetAsync(CatchId, Arg.Any<CancellationToken>());
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
        var action = () => sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
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
        var action = () => sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
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
        var action = () => sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
        await CatchClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }
}
