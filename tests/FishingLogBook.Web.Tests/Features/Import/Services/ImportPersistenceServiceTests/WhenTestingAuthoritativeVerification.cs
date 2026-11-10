using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportPersistenceServiceTests;

public class WhenTestingAuthoritativeVerification : BaseImportPersistenceServiceTest
{
    [Fact]
    public async Task ItShouldStopWhenTheServerCreatesATripWithADifferentIdentity()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<TripDto>() with { Id = Guid.NewGuid() });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheServerReturnsNoTripForACreateRequest()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns((TripDto?)null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheServerReturnsNoCatchForACreateRequest()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>()).Returns((CatchDto?)null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Catch);
        await CatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheServerCreatesTheCatchWithADifferentIdentity()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<CatchDto>() with { Id = Guid.NewGuid() });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await CatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenAnInvitedParticipantIsMissingFromTheAuthoritativeTrip()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();
        ParticipantClient.InviteAsync(TripId, Arg.Any<InviteTripParticipantDto>(), Arg.Any<CancellationToken>())
            .Returns(new TripParticipantsDto(TripId, "Owner") { Participants = [] });
        ParticipantClient.GetAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripParticipantsDto(TripId, "Owner") { Participants = [] });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenAnImportedPhotographHasNoPreparedBytes()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        batch.Photos.Single().SetPreparation(ImportPhotoPreparationStatusEnum.Ready, null, "blob:thumb");
        var sut = CreateSut();

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await CatchClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }
}
