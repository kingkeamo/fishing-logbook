using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportPersistenceServiceTests;

public sealed class WhenTestingExistingAuthoritativeState : BaseImportPersistenceServiceTest
{
    private static CatchLocationExposureDto Relocate(
        CatchLocationExposureDto existing,
        double? latitude = null,
        double? longitude = null,
        DateTimeOffset? capturedOn = null,
        string? source = null,
        string? visibility = null)
    {
        return new CatchLocationExposureDto
        {
            Visibility = visibility ?? existing.Visibility,
            Mode = existing.Mode,
            Latitude = latitude ?? existing.Latitude,
            Longitude = longitude ?? existing.Longitude,
            AccuracyMetres = existing.AccuracyMetres,
            CapturedOn = capturedOn ?? existing.CapturedOn,
            Source = source ?? existing.Source
        };
    }

    public static TheoryData<string, Func<CatchViewDto, CatchViewDto>> MismatchingCatches
    {
        get
        {
            return new TheoryData<string, Func<CatchViewDto, CatchViewDto>>
            {
                { "id", view => view with { Id = Guid.NewGuid() } },
                { "caught on", view => view with { CaughtOn = view.CaughtOn.AddMinutes(1) } },
                { "angler", view => view with { CaughtByUserId = Guid.NewGuid() } },
                { "recorder", view => view with { RecordedByUserId = Guid.NewGuid() } },
                { "trip", view => view with { TripId = Guid.NewGuid() } },
                { "species", view => view with { SpeciesName = "Pike" } },
                { "method", view => view with { Method = "Spinning" } },
                { "weight", view => view with { Weight = 99m } },
                { "length", view => view with { Length = 1m } },
                { "place name", view => view with { PlaceName = "Elsewhere" } },
                { "no location", view => view with { Location = null } },
                { "latitude", view => view with { Location = Relocate(view.Location!, latitude: 10) } },
                { "longitude", view => view with { Location = Relocate(view.Location!, longitude: 10) } },
                { "location captured on", view => view with { Location = Relocate(view.Location!, capturedOn: view.CaughtOn.AddDays(1)) } },
                { "location source", view => view with { Location = Relocate(view.Location!, source: "Manual") } },
                { "location visibility", view => view with { Location = Relocate(view.Location!, visibility: "Public") } }
            };
        }
    }

    public static TheoryData<string, Func<TripViewDto, TripViewDto>> MismatchingTrips
    {
        get
        {
            return new TheoryData<string, Func<TripViewDto, TripViewDto>>
            {
                { "id", view => view with { Id = Guid.NewGuid() } },
                { "owner", view => view with { OwnerUserId = Guid.NewGuid() } },
                { "status", view => view with { Status = "Planned" } },
                { "started on", view => view with { StartedOn = view.StartedOn.AddHours(1) } },
                { "ended on", view => view with { EndedOn = view.StartedOn.AddHours(9) } },
                { "title", view => view with { Title = "Different" } },
                { "place name", view => view with { PlaceName = "Elsewhere" } },
                { "location", view => view with { Location = new TripLocationDto(1, 1, null, view.StartedOn, "PhotoMetadata", "Private", "v1") } }
            };
        }
    }

    private async Task<(FishingLogBook.Web.Features.Import.Services.ImportPersistenceService Sut, ImportBatchModelHolder Holder, CatchDto Sent, CatchViewDto Authoritative)>
        PersistOnceAsync()
    {
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        var first = await sut.PersistAsync(batch, CancellationToken.None);
        first.IsSuccess.Should().BeTrue();
        var sent = (CatchDto)CatchClient.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(CatchClient.UpsertAsync))
            .GetArguments()[0]!;
        var authoritative = (await CatchClient.GetAsync(CatchId, CancellationToken.None))!;
        CatchClient.ClearReceivedCalls();
        return (sut, new ImportBatchModelHolder(batch), sent, authoritative);
    }

    [Theory]
    [MemberData(nameof(MismatchingCatches))]
    public async Task ItShouldRefuseToReuseAnExistingCatchThatDiffersFromTheProposal(
        string field,
        Func<CatchViewDto, CatchViewDto> change)
    {
        // Arrange
        var (sut, holder, _, authoritative) = await PersistOnceAsync();
        CatchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(change(authoritative));

        // Act
        var result = await sut.PersistAsync(holder.Batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse($"a differing {field} must not be reused");
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAnExistingPhotographWithADifferentContentType()
    {
        // Arrange
        var (sut, holder, _, authoritative) = await PersistOnceAsync();
        CatchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(authoritative with
        {
            Photographs = [new CatchPhotographViewDto(PhotoId, "image/png", "https://photo.test")]
        });

        // Act
        var result = await sut.PersistAsync(holder.Batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await CatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotUploadAPhotographThatIsAlreadyRegisteredWithAUrl()
    {
        // Arrange
        var (sut, holder, _, authoritative) = await PersistOnceAsync();
        CatchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(authoritative with
        {
            Photographs = [new CatchPhotographViewDto(PhotoId, "IMAGE/JPEG", "https://photo.test")]
        });

        // Act
        var result = await sut.PersistAsync(holder.Batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().RecordPhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<RecordPhotographDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheCatchCreateResponseIsMissing()
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
        await CatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheCatchCannotBeReadBackAfterCreation()
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
        await CatchClient.Received(1).UpsertAsync(
            Arg.Is<CatchDto>(dto => dto.Id == CatchId),
            Arg.Any<CancellationToken>());
        await CatchClient.Received(2).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheCatchRelationshipToTheTripIsWrongInTheCreateResponse()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.NoTrip);
        var sut = CreateSut();
        CatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<CatchDto>() with { TripId = Guid.NewGuid() });

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

    [Theory]
    [MemberData(nameof(MismatchingTrips))]
    public async Task ItShouldRefuseToReuseAnExistingTripThatDiffersFromTheProposal(
        string field,
        Func<TripViewDto, TripViewDto> change)
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        var first = await sut.PersistAsync(batch, CancellationToken.None);
        first.IsSuccess.Should().BeTrue();
        var existing = (await TripClient.GetDetailAsync(TripId, CancellationToken.None))!;
        TripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripDetailDto(change(existing.Trip)));
        TripClient.ClearReceivedCalls();
        CatchClient.ClearReceivedCalls();

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse($"a differing {field} must not be reused");
        await TripClient.DidNotReceive().UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheNewTripCreateResponseIsMissing()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns((TripDto?)null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Trip);
        await TripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Id == TripId),
            Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheNewTripCannotBeReadBackAfterCreation()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew);
        var sut = CreateSut();
        TripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns((TripDetailDto?)null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await TripClient.Received(1).UpsertAsync(
            Arg.Is<TripDto>(trip => trip.Id == TripId),
            Arg.Any<CancellationToken>());
        await CatchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheParticipantsCannotBeReadBeforeInviting()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();
        ParticipantClient.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns((TripParticipantsDto?)null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await ParticipantClient.DidNotReceive().InviteAsync(
            Arg.Any<Guid>(),
            Arg.Any<InviteTripParticipantDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotInviteAParticipantWhoIsAlreadyOnTheTrip()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();
        ParticipantClient.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(new TripParticipantsDto(TripId, "Owner")
        {
            Participants = [new TripParticipantDto(ParticipantId, "Invited", "Angler", null, CaughtOn)]
        });

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ParticipantCount.Should().Be(0);
        await ParticipantClient.DidNotReceive().InviteAsync(
            Arg.Any<Guid>(),
            Arg.Any<InviteTripParticipantDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheInviteResponseIsMissing()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();
        ParticipantClient.InviteAsync(TripId, Arg.Any<InviteTripParticipantDto>(), Arg.Any<CancellationToken>())
            .Returns((TripParticipantsDto?)null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Trip);
        await ParticipantClient.Received(1).InviteAsync(
            TripId,
            Arg.Is<InviteTripParticipantDto>(invite => invite.UserId == ParticipantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStopWhenTheParticipantsCannotBeVerifiedAfterInviting()
    {
        // Arrange
        var batch = Batch(ImportTripDecisionEnum.CreateNew, participant: true);
        var sut = CreateSut();
        var reads = 0;
        ParticipantClient.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(_ =>
            ++reads == 1 ? new TripParticipantsDto(TripId, "Owner") { Participants = [] } : null);

        // Act
        var result = await sut.PersistAsync(batch, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ImportPersistenceFailureEnum.Verification);
        await ParticipantClient.Received(1).InviteAsync(
            TripId,
            Arg.Is<InviteTripParticipantDto>(invite => invite.UserId == ParticipantId),
            Arg.Any<CancellationToken>());
        await ParticipantClient.Received(2).GetAsync(TripId, Arg.Any<CancellationToken>());
    }

    private sealed record ImportBatchModelHolder(FishingLogBook.Web.Features.Import.Models.ImportBatchModel Batch);
}
