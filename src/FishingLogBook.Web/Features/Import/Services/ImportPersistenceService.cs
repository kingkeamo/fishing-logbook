using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Users.Clients;

namespace FishingLogBook.Web.Features.Import.Services;

public sealed class ImportPersistenceService : IImportPersistenceService
{
    private readonly ITripClient _tripClient;
    private readonly ITripParticipantClient _participantClient;
    private readonly ICatchClient _catchClient;
    private readonly ICurrentUserClient _currentUserClient;
    private readonly IImportPhotoBlobRegistryService _blobRegistry;

    public ImportPersistenceService(
        ITripClient tripClient,
        ITripParticipantClient participantClient,
        ICatchClient catchClient,
        ICurrentUserClient currentUserClient,
        IImportPhotoBlobRegistryService blobRegistry)
    {
        _tripClient = tripClient;
        _participantClient = participantClient;
        _catchClient = catchClient;
        _currentUserClient = currentUserClient;
        _blobRegistry = blobRegistry;
    }

    public async Task<ImportPersistenceResultModel> PersistAsync(
        ImportBatchModel batch,
        CancellationToken cancellationToken,
        IProgress<ImportPersistenceProgressModel>? progress = null)
    {
        Validate(batch);
        var currentUser = await _currentUserClient.GetCurrentAsync(cancellationToken);
        var tripIds = new List<Guid>();
        var catchIds = new List<Guid>();
        var participantCount = 0;
        var photographCount = 0;
        var tripByCatch = new Dictionary<Guid, Guid>();
        var tripProposals = batch.TripProposals.Where(proposal => !proposal.IsRemoved).ToArray();
        var catchProposals = batch.CatchProposals.Where(proposal => !proposal.IsRemoved).ToArray();
        var photographTotal = catchProposals.Sum(proposal => proposal.PhotoIds.Count);
        var tripNumber = 0;
        var catchNumber = 0;
        var photographNumber = 0;

        foreach (var proposal in tripProposals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ImportPersistenceProgressModel(
                ImportPersistenceStageEnum.SavingTrip,
                ++tripNumber,
                tripProposals.Length));
            var tripId = proposal.Decision == ImportTripDecisionEnum.CreateNew
                ? await CreateTripAsync(batch, proposal, currentUser.UserId, cancellationToken)
                : proposal.ExistingTripId;
            if (tripId.HasValue)
            {
                if (proposal.Decision == ImportTripDecisionEnum.CreateNew)
                {
                    tripIds.Add(tripId.Value);
                }

                foreach (var catchId in proposal.CatchProposalIds)
                {
                    tripByCatch.Add(catchId, tripId.Value);
                }
            }

            if (proposal.Decision == ImportTripDecisionEnum.CreateNew)
            {
                participantCount += await PersistParticipantsAsync(
                    proposal,
                    currentUser.UserId,
                    cancellationToken);
                await RequireTripAsync(batch, proposal, currentUser.UserId, cancellationToken);
            }
        }

        foreach (var proposal in catchProposals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ImportPersistenceProgressModel(
                ImportPersistenceStageEnum.SavingCatch,
                ++catchNumber,
                catchProposals.Length));
            tripByCatch.TryGetValue(proposal.Id, out var tripId);
            var persisted = await PersistCatchAsync(
                batch,
                proposal,
                currentUser.UserId,
                tripId == Guid.Empty ? null : tripId,
                cancellationToken,
                progress,
                photographTotal,
                () => ++photographNumber);
            catchIds.Add(persisted.Id);
            photographCount += persisted.Photographs.Count;
        }

        progress?.Report(new ImportPersistenceProgressModel(
            ImportPersistenceStageEnum.Verifying,
            1,
            1));

        return new ImportPersistenceResultModel(
            tripIds.Distinct().ToArray(),
            catchIds,
            photographCount,
            participantCount);
    }

    private async Task<Guid> CreateTripAsync(
        ImportBatchModel batch,
        ImportTripProposalModel proposal,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var trip = new TripDto(
            proposal.Id,
            proposal.ProposedStatus,
            TripStartedOn(batch, proposal),
            TripEndedOn(batch, proposal),
            ToTripLocation(batch, proposal))
        {
            OwnerUserId = ownerUserId,
            Title = proposal.ProposedTitle,
            PlaceName = proposal.ProposedPlaceName
        };
        var existing = await _tripClient.GetDetailAsync(proposal.Id, cancellationToken);
        if (existing?.Trip is not null)
        {
            EnsureExpectedTrip(existing.Trip, trip);
            return existing.Trip.Id;
        }

        var persisted = await _tripClient.UpsertAsync(trip, cancellationToken)
            ?? throw new InvalidOperationException("The authoritative Trip create response was missing.");
        if (persisted.Id != proposal.Id)
        {
            throw new InvalidOperationException("The authoritative Trip identity did not match the Import proposal.");
        }

        return persisted.Id;
    }

    private async Task<int> PersistParticipantsAsync(
        ImportTripProposalModel proposal,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var current = await _participantClient.GetAsync(proposal.Id, cancellationToken)
            ?? throw new InvalidOperationException("The new Trip participants could not be read.");
        var existing = current.Participants.Select(participant => participant.UserId).ToHashSet();
        existing.Add(ownerUserId);
        var added = 0;
        foreach (var participant in proposal.Participants.DistinctBy(participant => participant.UserId))
        {
            if (existing.Contains(participant.UserId))
            {
                continue;
            }

            var response = await _participantClient.InviteAsync(
                proposal.Id,
                new InviteTripParticipantDto(participant.UserId),
                cancellationToken);
            if (response is null)
            {
                throw new InvalidOperationException("A selected Trip participant could not be invited.");
            }

            existing.Add(participant.UserId);
            added++;
        }

        var verified = await _participantClient.GetAsync(proposal.Id, cancellationToken)
            ?? throw new InvalidOperationException("The new Trip participants could not be verified.");
        if (proposal.Participants.Any(participant =>
                participant.UserId != ownerUserId
                && verified.Participants.All(actual => actual.UserId != participant.UserId)))
        {
            throw new InvalidOperationException("The authoritative Trip participant state is incomplete.");
        }

        return added;
    }

    private async Task<CatchViewDto> PersistCatchAsync(
        ImportBatchModel batch,
        ImportCatchProposalModel proposal,
        Guid userId,
        Guid? tripId,
        CancellationToken cancellationToken,
        IProgress<ImportPersistenceProgressModel>? progress,
        int photographTotal,
        Func<int> nextPhotographNumber)
    {
        var caughtOn = ToInstant(proposal.CaughtOn);
        var location = ToLocation(proposal.Location, caughtOn);
        var photographs = proposal.PhotoIds
            .Select(photoId => batch.Photos.Single(photo => photo.Id == photoId && !photo.IsRemoved))
            .Select(photo => new CatchPhotographDto(photo.Id, proposal.Id, photo.ContentType))
            .ToArray();
        var request = new CatchDto(proposal.Id, caughtOn, photographs, location)
        {
            CaughtByUserId = userId,
            RecordedByUserId = userId,
            TripId = tripId,
            SpeciesName = proposal.Species.Name,
            Method = proposal.Method.Name,
            Weight = proposal.Weight,
            Length = proposal.Length
        };
        var current = await _catchClient.GetAsync(proposal.Id, cancellationToken);
        if (current is null)
        {
            var persisted = await _catchClient.UpsertAsync(request, cancellationToken)
                ?? throw new InvalidOperationException("The authoritative Catch create response was missing.");
            if (persisted.Id != proposal.Id || persisted.TripId != tripId)
            {
                throw new InvalidOperationException("The authoritative Catch identity or Trip relationship is incorrect.");
            }

            current = await RequireCatchAsync(proposal.Id, cancellationToken);
        }
        else if (!HasExpectedCatch(current, request))
        {
            throw new InvalidOperationException("An authoritative Catch conflicts with the Import proposal.");
        }

        foreach (var photoId in proposal.PhotoIds)
        {
            progress?.Report(new ImportPersistenceProgressModel(
                ImportPersistenceStageEnum.UploadingPhotograph,
                nextPhotographNumber(),
                photographTotal));
            if (current.Photographs.Any(photo => photo.Id == photoId))
            {
                continue;
            }

            var photo = batch.Photos.Single(candidate => candidate.Id == photoId && !candidate.IsRemoved);
            if (string.IsNullOrWhiteSpace(photo.BlobToken))
            {
                throw new InvalidOperationException("An imported photograph has no prepared bytes.");
            }

            var bytes = await _blobRegistry.GetBytesAsync(photo.BlobToken, cancellationToken);
            var upload = await _catchClient.CreatePhotographUploadAsync(
                proposal.Id,
                new PhotographUploadRequestDto(photo.Id, photo.ContentType),
                cancellationToken);
            await _catchClient.UploadPhotographAsync(upload.UploadUrl, bytes, photo.ContentType, cancellationToken);
            await _catchClient.RecordPhotographAsync(
                proposal.Id,
                new RecordPhotographDto(photo.Id, upload.ObjectKey, photo.ContentType),
                cancellationToken);
            current = await RequireCatchAsync(proposal.Id, cancellationToken);
        }

        if (!HasExpectedCatch(current, request)
            || proposal.PhotoIds.Any(photoId => current.Photographs.All(photo => photo.Id != photoId)))
        {
            throw new InvalidOperationException("The authoritative Catch state is incomplete.");
        }

        return current;
    }

    private static void EnsureExpectedTrip(TripViewDto current, TripDto expected)
    {
        if (current.Id != expected.Id
            || current.OwnerUserId != expected.OwnerUserId
            || current.Status != expected.Status
            || current.StartedOn != expected.StartedOn
            || current.EndedOn != expected.EndedOn
            || !string.Equals(current.Title, expected.Title, StringComparison.Ordinal)
            || !string.Equals(current.PlaceName, expected.PlaceName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An authoritative Trip conflicts with the Import proposal.");
        }
    }

    private static bool HasExpectedCatch(CatchViewDto current, CatchDto expected)
    {
        return current.Id == expected.Id
            && current.CaughtOn == expected.CaughtOn
            && current.CaughtByUserId == expected.CaughtByUserId
            && current.RecordedByUserId == expected.RecordedByUserId
            && current.TripId == expected.TripId
            && string.Equals(current.SpeciesName, expected.SpeciesName, StringComparison.Ordinal)
            && string.Equals(current.Method, expected.Method, StringComparison.Ordinal)
            && current.Weight == expected.Weight
            && current.Length == expected.Length
            && HasExpectedLocation(current.Location, expected.Location);
    }

    private static bool HasExpectedLocation(CatchLocationExposureDto? current, CatchLocationDto? expected)
    {
        if (current is null || expected is null)
        {
            return current is null && expected is null;
        }

        return current.Latitude == expected.Latitude
            && current.Longitude == expected.Longitude
            && current.CapturedOn == expected.CapturedOn
            && string.Equals(current.Source, expected.Source, StringComparison.Ordinal)
            && string.Equals(current.Visibility, expected.Visibility, StringComparison.Ordinal);
    }

    private async Task RequireTripAsync(
        ImportBatchModel batch,
        ImportTripProposalModel expected,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var detail = await _tripClient.GetDetailAsync(expected.Id, cancellationToken);
        var trip = detail?.Trip;
        if (trip is null
            || trip.Id != expected.Id
            || trip.OwnerUserId != ownerUserId
            || trip.Status != expected.ProposedStatus
            || trip.StartedOn != TripStartedOn(batch, expected)
            || trip.EndedOn != TripEndedOn(batch, expected))
        {
            throw new InvalidOperationException("The authoritative Trip could not be verified.");
        }
    }

    private static TripLocationDto? ToTripLocation(
        ImportBatchModel batch,
        ImportTripProposalModel proposal)
    {
        var location = proposal.RepresentativeLocation;
        if (location is not { Decision: ImportLocationDecisionEnum.Accepted, HasCanonicalCoordinates: true })
        {
            return null;
        }

        return new TripLocationDto(
            location.Latitude!.Value,
            location.Longitude!.Value,
            null,
            TripStartedOn(batch, proposal),
            LocationDefaults.PhotoMetadata,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }

    private static DateTimeOffset TripStartedOn(
        ImportBatchModel batch,
        ImportTripProposalModel proposal)
    {
        return proposal.CatchProposalIds
            .Select(catchId => batch.CatchProposals.Single(candidate => candidate.Id == catchId))
            .Min(candidate => ToInstant(candidate.CaughtOn));
    }

    private static DateTimeOffset TripEndedOn(
        ImportBatchModel batch,
        ImportTripProposalModel proposal)
    {
        return proposal.CatchProposalIds
            .Select(catchId => batch.CatchProposals.Single(candidate => candidate.Id == catchId))
            .Max(candidate => ToInstant(candidate.CaughtOn));
    }

    private async Task<CatchViewDto> RequireCatchAsync(Guid catchId, CancellationToken cancellationToken)
    {
        return await _catchClient.GetAsync(catchId, cancellationToken)
            ?? throw new InvalidOperationException("The authoritative Catch could not be verified.");
    }

    private static CatchLocationDto? ToLocation(ImportLocationModel? location, DateTimeOffset caughtOn)
    {
        if (location is not { Decision: ImportLocationDecisionEnum.Accepted, HasCanonicalCoordinates: true })
        {
            return null;
        }

        return new CatchLocationDto(
            location.Latitude!.Value,
            location.Longitude!.Value,
            null,
            caughtOn,
            LocationDefaults.PhotoMetadata,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }

    private static DateTimeOffset ToInstant(ImportTimestampModel timestamp)
    {
        if (!timestamp.IsResolved)
        {
            throw new InvalidOperationException("Every imported Catch requires a reviewed historical date and time.");
        }

        return timestamp.Instant
            ?? throw new InvalidOperationException("Every imported Catch requires a deterministic historical instant with an explicitly confirmed UTC offset.");
    }

    private static void Validate(ImportBatchModel batch)
    {
        batch.ValidateForPersistence(DateTimeOffset.UtcNow);
    }
}
