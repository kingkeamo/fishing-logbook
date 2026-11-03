using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.Features.Photographs.Clients;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.Services.ImportPersistenceServiceTests;

public class BaseImportPersistenceServiceTest
{
    protected static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    protected static readonly Guid CatchId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    protected static readonly Guid PhotoId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    protected static readonly Guid SecondPhotoId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    protected static readonly Guid TripId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    protected static readonly Guid ParticipantId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    protected static readonly DateTimeOffset CaughtOn = DateTimeOffset.Parse("2009-02-02T15:06:00+01:00");

    protected ITripClient TripClient { get; } = Substitute.For<ITripClient>();
    protected ITripParticipantClient ParticipantClient { get; } = Substitute.For<ITripParticipantClient>();
    protected ICatchSyncClient CatchSync { get; } = Substitute.For<ICatchSyncClient>();
    protected ICatchRemoteClient CatchRemote { get; } = Substitute.For<ICatchRemoteClient>();
    protected IPhotographUploadClient Uploads { get; } = Substitute.For<IPhotographUploadClient>();
    protected IAccountSessionCoordinator Session { get; } = Substitute.For<IAccountSessionCoordinator>();
    protected IImportPhotoBlobRegistryService BlobRegistry { get; } = Substitute.For<IImportPhotoBlobRegistryService>();

    protected ImportPersistenceService CreateSut()
    {
        Session.Capture().Returns(new AccountSessionLeaseModel(UserId, 1, CancellationToken.None));
        BlobRegistry.GetBytesAsync("token", Arg.Any<CancellationToken>()).Returns([1, 2, 3]);
        TripDto? persistedTrip = null;
        TripClient.UpsertAsync(Arg.Any<TripDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            persistedTrip = call.Arg<TripDto>();
            return ApiResultModel<TripDto>.Succeeded(persistedTrip);
        });
        TripClient.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(_ =>
            persistedTrip is null
                ? ApiResultModel<TripDetailDto>.Failed(ApiOutcomeEnum.NotFound)
                : ApiResultModel<TripDetailDto>.Succeeded(new TripDetailDto(new TripViewDto(
                    TripId,
                    UserId,
                    persistedTrip.Status,
                    persistedTrip.StartedOn,
                    persistedTrip.EndedOn)
                {
                    Title = persistedTrip.Title,
                    PlaceName = persistedTrip.PlaceName
                })));
        var participantReads = 0;
        ParticipantClient.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            participantReads++;
            return ApiResultModel<TripParticipantsDto>.Succeeded(new TripParticipantsDto(TripId, "Owner")
            {
                Participants = participantReads > 1
                    ? [new TripParticipantDto(ParticipantId, "Invited", "Angler", null, CaughtOn)]
                    : []
            });
        });
        ParticipantClient.InviteAsync(TripId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripParticipantsDto>.Succeeded(new TripParticipantsDto(TripId, "Owner")
            {
                Participants = [new TripParticipantDto(ParticipantId, "Invited", "Angler", null, CaughtOn)]
            }));
        CatchDto? persistedCatch = null;
        CatchSync.UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            persistedCatch = call.Arg<CatchDto>();
            return ApiResultModel<CatchDto>.Succeeded(persistedCatch);
        });
        CatchSync.CreatePhotographUploadAsync(
                CatchId,
                Arg.Any<PhotographUploadRequestDto>(),
                Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<PhotographUploadDto>.Succeeded(
                new PhotographUploadDto("object", "https://upload.test")));
        Uploads.UploadAsync(
                Arg.Any<string>(),
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(PhotographUploadOutcomeEnum.Uploaded);
        var photographRecorded = false;
        CatchSync.RecordPhotographAsync(
                CatchId,
                Arg.Any<RecordPhotographDto>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                photographRecorded = true;
                return ApiResultModel<NoContentModel>.Succeeded(NoContentModel.Instance);
            });
        CatchRemote.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (persistedCatch is null)
            {
                return ApiResultModel<CatchViewDto>.Failed(ApiOutcomeEnum.NotFound);
            }

            return ApiResultModel<CatchViewDto>.Succeeded(new CatchViewDto(
                CatchId,
                UserId,
                persistedCatch.CaughtOn,
                new CatchLocationExposureDto
                {
                    Latitude = 53.1,
                    Longitude = -6.2,
                    CapturedOn = persistedCatch.CaughtOn,
                    Source = LocationDefaults.PhotoMetadata,
                    Visibility = LocationDefaults.Private
                })
            {
                RecordedByUserId = UserId,
                TripId = persistedCatch.TripId,
                SpeciesName = persistedCatch.SpeciesName,
                Method = persistedCatch.Method,
                Weight = persistedCatch.Weight,
                Length = persistedCatch.Length,
                PlaceName = persistedCatch.PlaceName,
                Photographs =
                [
                    new CatchPhotographViewDto(
                        PhotoId,
                        "image/jpeg",
                        photographRecorded ? "https://photo.test" : null)
                ]
            });
        });
        return new ImportPersistenceService(
            TripClient,
            ParticipantClient,
            CatchSync,
            CatchRemote,
            Uploads,
            Session,
            BlobRegistry);
    }

    protected static ImportBatchModel Batch(
        ImportTripDecisionEnum decision,
        bool participant = false,
        ImportTimestampModel? timestamp = null,
        bool includeSecondPhoto = false)
    {
        var method = new ImportCatalogueSelectionModel(Guid.NewGuid(), "Fly", "Fly");
        var species = new ImportCatalogueSelectionModel(Guid.NewGuid(), "BrownTrout", "Brown Trout");
        var batch = new ImportBatchModel(Guid.NewGuid(), method, species);
        var photo = new ImportSelectedPhotoModel(PhotoId, 0, "image/jpeg", 3, "token", "fish.jpg", "blob:thumb");
        photo.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, "token", "blob:thumb");
        batch.AddPhoto(photo);
        if (includeSecondPhoto)
        {
            var second = new ImportSelectedPhotoModel(
                SecondPhotoId,
                1,
                "image/png",
                3,
                "token",
                "fish-2.png",
                "blob:thumb-2");
            second.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, "token", "blob:thumb-2");
            batch.AddPhoto(second);
        }

        var lookup = new ImportLocationLookupResultModel(["River Corrib", "Galway", "Ireland"])
        {
            Street = "River Corrib",
            Locality = "Galway",
            Country = "Ireland"
        };
        var location = new ImportLocationModel(53.1, -6.2, true)
            .WithLookup(ImportLocationLookupStatusEnum.Resolved, lookup)
            .Accept();
        var proposal = new ImportCatchProposalModel(
            CatchId,
            includeSecondPhoto ? [PhotoId, SecondPhotoId] : [PhotoId],
            timestamp ?? ImportTimestampModel.UserConfirmed(CaughtOn),
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
            proposal.CaughtOn.Instant!.Value.DateTime,
            proposal.CaughtOn.Instant.Value.DateTime);
        trip.Decide(decision, decision == ImportTripDecisionEnum.UseExisting ? TripId : null);
        if (participant)
        {
            trip.AddParticipant(new AnglerSummaryDto(ParticipantId, "Angler", null, null, null));
        }

        batch.AddTripProposal(trip);
        return batch;
    }
}
