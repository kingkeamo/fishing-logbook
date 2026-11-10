using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.Services.ImportPersistenceServiceTests;

public class WhenTestingAuthoritativeVerification : BaseImportPersistenceServiceTest
{
    [Fact]
    public async Task ItShouldRefuseToPersistWithoutAnAuthenticatedSession()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        Session.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await TripClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheServerCreatesATripWithADifferentIdentity()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ApiResultModel<TripDto>.Succeeded(call.Arg<TripDto>() with { Id = Guid.NewGuid() }));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheTripCreateRequestIsRejected()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripDto>.Failed(ApiOutcomeEnum.ServerError));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Trip);
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenAParticipantInviteIsRejected()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();
        ParticipantClient.InviteAsync(TripId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripParticipantsDto>.Failed(ApiOutcomeEnum.ServerError));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Trip);
        await ParticipantClient.Received(1).InviteAsync(TripId, ParticipantId, Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenAnInvitedParticipantIsMissingFromTheAuthoritativeTrip()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();
        ParticipantClient.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripParticipantsDto>.Succeeded(
                new TripParticipantsDto(TripId, "Owner") { Participants = [] }));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await ParticipantClient.Received(1).InviteAsync(TripId, ParticipantId, Arg.Any<CancellationToken>());
        await ParticipantClient.Received(2).GetAsync(TripId, Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheAuthoritativeTripDoesNotMatchWhatWasSent()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripDetailDto>.Succeeded(new TripDetailDto(new TripViewDto(
                TripId,
                UserId,
                "Completed",
                CaughtOn.AddDays(-30)))));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await TripClient.Received(1).GetAsync(TripId, Arg.Any<CancellationToken>());
        await TripClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheServerCreatesTheCatchWithADifferentIdentity()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchSync.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ApiResultModel<CatchDto>.Succeeded(call.Arg<CatchDto>() with { Id = Guid.NewGuid() }));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Catch);
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheCatchCreateRequestIsRejected()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchSync.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<CatchDto>.Failed(ApiOutcomeEnum.ServerError));

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Catch);
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSendAnythingWhenAnImportedPhotographHasNoPreparedBytes()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        batch.Photos.Single().SetPreparation(ImportPhotoPreparationStatusEnum.Ready, null, "blob:thumb");
        var sut = CreateSut();

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await TripClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await BlobRegistry.DidNotReceive().GetBytesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await CatchSync.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheAuthoritativeCatchLocationDoesNotMatch()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        ArrangeServerStoringCatchWithLocation(new CatchLocationExposureDto
        {
            Latitude = 10,
            Longitude = 10,
            CapturedOn = CaughtOn,
            Source = LocationDefaults.PhotoMetadata,
            Visibility = LocationDefaults.Private
        });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId
                && dto.Location != null
                && dto.Location.Latitude == 53.1
                && dto.Location.Longitude == -6.2),
            Arg.Any<CancellationToken>());
        await CatchRemote.Received(3).GetAsync(CatchId, Arg.Any<CancellationToken>());
        await CatchSync.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheAuthoritativeCatchHasNoLocationButOneWasSent()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        ArrangeServerStoringCatchWithLocation(null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchSync.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId && dto.Location != null),
            Arg.Any<CancellationToken>());
        await CatchRemote.Received(3).GetAsync(CatchId, Arg.Any<CancellationToken>());
        await CatchSync.Received(1).RecordPhotographAsync(
            CatchId,
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    private void ArrangeServerStoringCatchWithLocation(CatchLocationExposureDto? storedLocation)
    {
        CatchDto? sent = null;
        var recorded = false;
        CatchSync.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            sent = call.Arg<CatchDto>();
            return ApiResultModel<CatchDto>.Succeeded(sent);
        });
        CatchSync.RecordPhotographAsync(CatchId, Arg.Any<RecordPhotographDto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                recorded = true;
                return ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance);
            });
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(_ =>
            sent is null
                ? ApiResultModel<CatchViewDto>.Failed(ApiOutcomeEnum.NotFound)
                : ApiResultModel<CatchViewDto>.Succeeded(new CatchViewDto(CatchId, UserId, sent.CaughtOn, storedLocation)
                {
                    RecordedByUserId = UserId,
                    SpeciesName = sent.SpeciesName,
                    Method = sent.Method,
                    Weight = sent.Weight,
                    Length = sent.Length,
                    PlaceName = sent.PlaceName,
                    Photographs = [new CatchPhotographViewDto(PhotoId, "image/jpeg", recorded ? "https://photo.test" : null)]
                }));
    }
}
