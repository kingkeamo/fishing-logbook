using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Users.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportPersistenceServiceTests;

public class BaseImportPersistenceServiceTest
{
    protected static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    protected static readonly Guid CatchId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    protected static readonly Guid PhotoId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    protected static readonly Guid TripId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    protected static readonly Guid ParticipantId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    protected static readonly DateTimeOffset CaughtOn = DateTimeOffset.Parse("2009-02-02T15:06:00+01:00");

    protected ITripClient TripClient { get; } = Substitute.For<ITripClient>();
    protected ITripParticipantClient ParticipantClient { get; } = Substitute.For<ITripParticipantClient>();
    protected ICatchClient CatchClient { get; } = Substitute.For<ICatchClient>();
    protected ICurrentUserClient CurrentUserClient { get; } = Substitute.For<ICurrentUserClient>();
    protected IImportPhotoBlobRegistryService BlobRegistry { get; } = Substitute.For<IImportPhotoBlobRegistryService>();

    protected ImportPersistenceService CreateSut()
    {
        CurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(new CurrentUserDto(UserId, "angler@example.test", "test", "subject"));
        BlobRegistry.GetBytesAsync("token", Arg.Any<CancellationToken>()).Returns([1, 2, 3]);
        TripDto? persistedTrip = null;
        TripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            persistedTrip = call.Arg<TripDto>();
            return persistedTrip;
        });
        TripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(_ =>
            new TripDetailDto(new TripViewDto(
                TripId,
                UserId,
                persistedTrip!.Status,
                persistedTrip.StartedOn,
                persistedTrip.EndedOn)));
        var participantReads = 0;
        ParticipantClient.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            participantReads++;
            return new TripParticipantsDto(TripId, "Owner")
            {
                Participants = participantReads > 1
                    ? [new TripParticipantDto(ParticipantId, "Invited", "Angler", null, CaughtOn)]
                    : []
            };
        });
        ParticipantClient.InviteAsync(TripId, Arg.Any<InviteTripParticipantDto>(), Arg.Any<CancellationToken>())
            .Returns(new TripParticipantsDto(TripId, "Owner")
            {
                Participants = [new TripParticipantDto(ParticipantId, "Invited", "Angler", null, CaughtOn)]
            });
        CatchDto? persistedCatch = null;
        CatchClient.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            persistedCatch = call.Arg<CatchDto>();
            return persistedCatch;
        });
        CatchClient.CreatePhotographUploadAsync(CatchId, Arg.Any<PhotographUploadRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new PhotographUploadDto("object", "https://upload.test"));
        var reads = 0;
        CatchClient.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            reads++;
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
                TripId = persistedCatch?.TripId,
                SpeciesName = persistedCatch?.SpeciesName,
                Method = persistedCatch?.Method,
                Weight = persistedCatch?.Weight,
                Length = persistedCatch?.Length,
                Photographs = reads > 1 ? [new CatchPhotographViewDto(PhotoId, "image/jpeg", "https://photo.test")] : []
            };
        });
        return new ImportPersistenceService(TripClient, ParticipantClient, CatchClient, CurrentUserClient, BlobRegistry);
    }

    protected static ImportBatchModel Batch(ImportTripDecisionEnum decision, bool participant = false)
    {
        var method = new ImportCatalogueSelectionModel(Guid.NewGuid(), "Fly", "Fly");
        var species = new ImportCatalogueSelectionModel(Guid.NewGuid(), "BrownTrout", "Brown Trout");
        var batch = new ImportBatchModel(Guid.NewGuid(), method, species);
        var photo = new ImportSelectedPhotoModel(PhotoId, 0, "image/jpeg", 3, "token", "fish.jpg", "blob:thumb");
        photo.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, "token", "blob:thumb");
        batch.AddPhoto(photo);
        var location = new ImportLocationModel(53.1, -6.2, true).Accept();
        var proposal = new ImportCatchProposalModel(
            CatchId,
            [PhotoId],
            ImportTimestampModel.UserConfirmed(CaughtOn),
            method,
            species,
            location,
            weight: 2.5m,
            length: 42m);
        proposal.MarkReviewed();
        batch.AddCatchProposal(proposal);
        var trip = new ImportTripProposalModel(
            TripId,
            [CatchId],
            ImportTripSuggestionConfidenceEnum.Strong,
            [],
            CaughtOn.DateTime,
            CaughtOn.DateTime);
        trip.Decide(decision, decision == ImportTripDecisionEnum.UseExisting ? TripId : null);
        if (participant)
        {
            trip.AddParticipant(new AnglerSummaryDto(ParticipantId, "Angler", null, null, null));
        }

        batch.AddTripProposal(trip);
        return batch;
    }
}
