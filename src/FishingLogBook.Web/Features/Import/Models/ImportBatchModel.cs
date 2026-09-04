using FishingLogBook.Web.Features.Import.Enums;

namespace FishingLogBook.Web.Features.Import.Models;

public sealed class ImportBatchModel
{
    private readonly List<ImportSelectedPhotoModel> _photos = [];
    private readonly List<ImportCatchProposalModel> _catchProposals = [];
    private readonly List<ImportTripProposalModel> _tripProposals = [];

    public ImportBatchModel(
        Guid id,
        ImportCatalogueSelectionModel fishingMethod,
        ImportCatalogueSelectionModel species)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An Import batch identity is required.", nameof(id));
        }

        Id = id;
        FishingMethod = fishingMethod;
        Species = species;
    }

    public Guid Id { get; }

    public ImportStageEnum Stage { get; private set; }

    public ImportCatalogueSelectionModel FishingMethod { get; private set; }

    public ImportCatalogueSelectionModel Species { get; private set; }

    public IReadOnlyList<ImportSelectedPhotoModel> Photos => _photos.OrderBy(photo => photo.SelectionIndex).ToArray();

    public IReadOnlyList<ImportCatchProposalModel> CatchProposals => _catchProposals;

    public IReadOnlyList<ImportTripProposalModel> TripProposals => _tripProposals;

    public bool IsCancelled { get; private set; }

    public bool IsProcessingPhotos { get; private set; }

    public bool CanProcessPhotos => FishingMethod.IsValid && Species.IsValid && !IsCancelled;

    public bool IsReadyForConfirmation
    {
        get
        {
            var activeCatches = _catchProposals.Where(proposal => !proposal.IsRemoved).ToArray();
            return CanProcessPhotos
                && activeCatches.Length > 0
                && activeCatches.All(proposal => proposal.IsReadyForConfirmation)
                && _tripProposals.All(proposal => proposal.IsDecisionComplete);
        }
    }

    public bool CanAdvanceToTrips
    {
        get
        {
            var active = _catchProposals.Where(proposal => !proposal.IsRemoved).ToArray();
            return active.Length > 0 && active.All(proposal => proposal.IsReadyForConfirmation);
        }
    }

    public void SetDefaults(
        ImportCatalogueSelectionModel fishingMethod,
        ImportCatalogueSelectionModel species)
    {
        FishingMethod = fishingMethod;
        Species = species;
    }

    public void SetStage(ImportStageEnum stage)
    {
        Stage = stage;
    }

    public void AddPhoto(ImportSelectedPhotoModel photo)
    {
        if (_photos.Any(existing => existing.Id == photo.Id || existing.SelectionIndex == photo.SelectionIndex))
        {
            throw new InvalidOperationException("Photo identity and selection order must be unique.");
        }

        _photos.Add(photo);
    }

    public void BeginPhotoProcessing()
    {
        if (!CanProcessPhotos)
        {
            throw new InvalidOperationException("Valid Fishing Method and Species defaults are required.");
        }

        IsProcessingPhotos = true;
    }

    public void CompletePhotoProcessing()
    {
        IsProcessingPhotos = false;
    }

    public void AddCatchProposal(ImportCatchProposalModel proposal)
    {
        if (_catchProposals.Any(existing => existing.Id == proposal.Id))
        {
            throw new InvalidOperationException("The Catch proposal already exists.");
        }

        var activePhotoIds = _photos.Where(photo => !photo.IsRemoved).Select(photo => photo.Id).ToHashSet();
        if (proposal.PhotoIds.Any(photoId => !activePhotoIds.Contains(photoId)))
        {
            throw new InvalidOperationException("Catch proposals may reference only active selected photos.");
        }

        var assignedPhotoIds = _catchProposals
            .Where(existing => !existing.IsRemoved)
            .SelectMany(existing => existing.PhotoIds)
            .ToHashSet();
        if (proposal.PhotoIds.Any(assignedPhotoIds.Contains))
        {
            throw new InvalidOperationException("A photo cannot belong to more than one active Catch proposal.");
        }

        _catchProposals.Add(proposal);
    }

    public void ReplaceCatchProposals(IEnumerable<ImportCatchProposalModel> proposals)
    {
        var replacements = proposals.ToArray();
        var activePhotoIds = _photos.Where(photo => !photo.IsRemoved).Select(photo => photo.Id).ToHashSet();
        if (replacements.Select(proposal => proposal.Id).Distinct().Count() != replacements.Length)
        {
            throw new InvalidOperationException("Catch proposal identities must be unique.");
        }

        var membership = replacements.SelectMany(proposal => proposal.PhotoIds).ToArray();
        if (membership.Any(photoId => !activePhotoIds.Contains(photoId)))
        {
            throw new InvalidOperationException("Catch proposals may reference only active selected photos.");
        }

        if (membership.Distinct().Count() != membership.Length)
        {
            throw new InvalidOperationException("A photo cannot belong to more than one active Catch proposal.");
        }

        _catchProposals.Clear();
        _catchProposals.AddRange(replacements);
        _tripProposals.Clear();
    }

    public void AddTripProposal(ImportTripProposalModel proposal)
    {
        if (_tripProposals.Any(existing => existing.Id == proposal.Id))
        {
            throw new InvalidOperationException("The Trip proposal already exists.");
        }

        var reviewedCatchIds = _catchProposals
            .Where(candidate => !candidate.IsRemoved
                && candidate.ReviewStatus == ImportCatchReviewStatusEnum.Reviewed)
            .Select(candidate => candidate.Id)
            .ToHashSet();
        if (proposal.CatchProposalIds.Any(catchId => !reviewedCatchIds.Contains(catchId)))
        {
            throw new InvalidOperationException("Trip proposals may reference only reviewed Catch proposals.");
        }

        var assignedCatchIds = _tripProposals.SelectMany(existing => existing.CatchProposalIds).ToHashSet();
        if (proposal.CatchProposalIds.Any(assignedCatchIds.Contains))
        {
            throw new InvalidOperationException("A Catch cannot belong to more than one Trip proposal.");
        }

        _tripProposals.Add(proposal);
    }

    public void RemovePhoto(Guid photoId)
    {
        var photo = _photos.SingleOrDefault(candidate => candidate.Id == photoId);
        if (photo is null)
        {
            return;
        }

        photo.Remove();
        foreach (var proposal in _catchProposals.Where(candidate => !candidate.IsRemoved))
        {
            proposal.RemovePhoto(photoId);
        }

        RemoveInactiveCatchMemberships();
        _tripProposals.Clear();
    }

    public void RemoveCatchProposal(Guid catchProposalId)
    {
        var proposal = _catchProposals.SingleOrDefault(candidate => candidate.Id == catchProposalId);
        if (proposal is null)
        {
            return;
        }

        foreach (var photoId in proposal.PhotoIds.ToArray())
        {
            _photos.Single(photo => photo.Id == photoId).Remove();
            proposal.RemovePhoto(photoId);
        }

        _tripProposals.Clear();
    }

    public void SetCatchCaughtOn(Guid catchProposalId, ImportTimestampModel caughtOn)
    {
        ActiveCatch(catchProposalId).SetCaughtOn(caughtOn);
        _tripProposals.Clear();
    }

    public void SetCatchMethod(Guid catchProposalId, ImportCatalogueSelectionModel method)
    {
        ActiveCatch(catchProposalId).OverrideMethod(method);
        _tripProposals.Clear();
    }

    public void SetCatchSpecies(Guid catchProposalId, ImportCatalogueSelectionModel species)
    {
        ActiveCatch(catchProposalId).OverrideSpecies(species);
        _tripProposals.Clear();
    }

    public void SetCatchLocation(Guid catchProposalId, ImportLocationModel? location)
    {
        ActiveCatch(catchProposalId).SetLocation(location);
        _tripProposals.Clear();
    }

    public void SetCatchWeight(Guid catchProposalId, decimal? weight)
    {
        ActiveCatch(catchProposalId).SetWeight(weight);
        _tripProposals.Clear();
    }

    public void SetCatchLength(Guid catchProposalId, decimal? length)
    {
        ActiveCatch(catchProposalId).SetLength(length);
        _tripProposals.Clear();
    }

    public void MarkCatchReviewed(Guid catchProposalId)
    {
        ActiveCatch(catchProposalId).MarkReviewed();
    }

    public void ConfirmDisplayedCatch(Guid catchProposalId)
    {
        ActiveCatch(catchProposalId).ConfirmDisplayedValues();
        _tripProposals.Clear();
    }

    public ImportCatchProposalModel SplitCatch(
        Guid catchProposalId,
        IEnumerable<Guid> selectedPhotoIds,
        Guid newCatchProposalId)
    {
        if (newCatchProposalId == Guid.Empty
            || _catchProposals.Any(proposal => proposal.Id == newCatchProposalId))
        {
            throw new InvalidOperationException("A split requires a new unique Catch proposal identity.");
        }

        var source = ActiveCatch(catchProposalId);
        var selected = selectedPhotoIds.Distinct().ToArray();
        if (selected.Length == 0
            || selected.Length >= source.PhotoIds.Count
            || selected.Any(photoId => !source.PhotoIds.Contains(photoId)))
        {
            throw new InvalidOperationException("A split must move some, but not all, photos from its source Catch.");
        }

        var ordered = OrderedPhotoIds(selected);
        foreach (var photoId in ordered)
        {
            source.RemovePhoto(photoId);
        }

        var firstPhoto = _photos.Single(photo => photo.Id == ordered[0]);
        var reasons = ReasonsFor(firstPhoto.Timestamp).ToList();
        var locations = ordered
            .Select(photoId => _photos.Single(photo => photo.Id == photoId).Location)
            .Where(location => location.HasCanonicalCoordinates)
            .DistinctBy(location => (location.Latitude, location.Longitude))
            .ToArray();
        if (locations.Length > 1)
        {
            reasons.Add(ImportCatchProposalReasonEnum.ConflictingGps);
        }

        var created = new ImportCatchProposalModel(
            newCatchProposalId,
            ordered,
            firstPhoto.Timestamp,
            source.Method,
            source.Species,
            locations.Length == 1 ? locations[0] : null,
            reasons);
        AddCatchProposal(created);
        SortCatchProposals();
        _tripProposals.Clear();
        return created;
    }

    public void MergeCatches(Guid primaryCatchProposalId, Guid absorbedCatchProposalId)
    {
        if (primaryCatchProposalId == absorbedCatchProposalId)
        {
            throw new InvalidOperationException("A Catch proposal cannot be merged with itself.");
        }

        var primary = ActiveCatch(primaryCatchProposalId);
        var absorbed = ActiveCatch(absorbedCatchProposalId);
        var conflicts = primary.CaughtOn != absorbed.CaughtOn
            || primary.Method != absorbed.Method
            || primary.Species != absorbed.Species
            || primary.Location != absorbed.Location;
        primary.SetPhotos(OrderedPhotoIds(primary.PhotoIds.Concat(absorbed.PhotoIds)));
        if (conflicts)
        {
            primary.RequireCanonicalReview();
        }

        absorbed.Remove();
        _tripProposals.Clear();
    }

    public void Cancel()
    {
        IsCancelled = true;
        IsProcessingPhotos = false;
    }

    private void RemoveInactiveCatchMemberships()
    {
        var inactiveCatchIds = _catchProposals
            .Where(proposal => proposal.IsRemoved)
            .Select(proposal => proposal.Id)
            .ToArray();
        foreach (var tripProposal in _tripProposals)
        {
            foreach (var catchId in inactiveCatchIds)
            {
                tripProposal.RemoveCatch(catchId);
            }
        }
    }

    private ImportCatchProposalModel ActiveCatch(Guid catchProposalId)
    {
        return _catchProposals.SingleOrDefault(proposal => proposal.Id == catchProposalId && !proposal.IsRemoved)
            ?? throw new InvalidOperationException("The active Catch proposal was not found.");
    }

    private Guid[] OrderedPhotoIds(IEnumerable<Guid> photoIds)
    {
        var selectionOrder = _photos.ToDictionary(photo => photo.Id, photo => photo.SelectionIndex);
        return photoIds.Distinct().OrderBy(photoId => selectionOrder[photoId]).ToArray();
    }

    private void SortCatchProposals()
    {
        var selectionOrder = _photos.ToDictionary(photo => photo.Id, photo => photo.SelectionIndex);
        _catchProposals.Sort((left, right) =>
            left.PhotoIds.Min(photoId => selectionOrder[photoId])
                .CompareTo(right.PhotoIds.Min(photoId => selectionOrder[photoId])));
    }

    private static IEnumerable<ImportCatchProposalReasonEnum> ReasonsFor(ImportTimestampModel timestamp)
    {
        yield return timestamp.State switch
        {
            ImportTimestampStateEnum.ExplicitInstant => ImportCatchProposalReasonEnum.TrustworthyCaptureTime,
            ImportTimestampStateEnum.LocalWallClock => ImportCatchProposalReasonEnum.AmbiguousTimestamp,
            ImportTimestampStateEnum.WeakFallback => ImportCatchProposalReasonEnum.WeakTimestamp,
            ImportTimestampStateEnum.Unusable => ImportCatchProposalReasonEnum.UnusableTimestamp,
            _ => ImportCatchProposalReasonEnum.MissingTimestamp
        };
    }
}
