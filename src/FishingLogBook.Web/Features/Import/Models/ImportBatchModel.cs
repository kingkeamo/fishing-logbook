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
    }

    public void RemoveCatchProposal(Guid catchProposalId)
    {
        var proposal = _catchProposals.SingleOrDefault(candidate => candidate.Id == catchProposalId);
        if (proposal is null)
        {
            return;
        }

        proposal.Remove();
        foreach (var tripProposal in _tripProposals)
        {
            tripProposal.RemoveCatch(catchProposalId);
        }
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
}
