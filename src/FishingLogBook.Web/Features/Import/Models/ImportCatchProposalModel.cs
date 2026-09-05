using FishingLogBook.Web.Features.Import.Enums;

namespace FishingLogBook.Web.Features.Import.Models;

public sealed class ImportCatchProposalModel
{
    private readonly List<Guid> _photoIds;
    private readonly IReadOnlyList<ImportCatchProposalReasonEnum> _reasons;
    private bool _canonicalReviewRequired;
    private bool _gpsConflictResolved;

    public ImportCatchProposalModel(
        Guid id,
        IEnumerable<Guid> photoIds,
        ImportTimestampModel caughtOn,
        ImportCatalogueSelectionModel inheritedMethod,
        ImportCatalogueSelectionModel inheritedSpecies,
        ImportLocationModel? location = null,
        IEnumerable<ImportCatchProposalReasonEnum>? reasons = null,
        decimal? weight = null,
        decimal? length = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A Catch proposal identity is required.", nameof(id));
        }

        _photoIds = photoIds.ToList();
        if (_photoIds.Count == 0 || _photoIds.Any(photoId => photoId == Guid.Empty))
        {
            throw new ArgumentException("A Catch proposal requires valid photo identities.", nameof(photoIds));
        }

        if (_photoIds.Count != _photoIds.Distinct().Count())
        {
            throw new ArgumentException("A Catch proposal cannot contain duplicate photos.", nameof(photoIds));
        }

        Id = id;
        CaughtOn = caughtOn;
        InheritedMethod = inheritedMethod;
        InheritedSpecies = inheritedSpecies;
        Location = location;
        Weight = weight;
        Length = length;
        _reasons = reasons?.Distinct().ToArray() ?? [];
        _gpsConflictResolved = !_reasons.Contains(ImportCatchProposalReasonEnum.ConflictingGps);
    }

    public Guid Id { get; }

    public IReadOnlyList<Guid> PhotoIds => _photoIds;

    public ImportTimestampModel CaughtOn { get; private set; }

    public ImportLocationModel? Location { get; private set; }

    public decimal? Weight { get; private set; }

    public decimal? Length { get; private set; }

    public IReadOnlyList<ImportCatchProposalReasonEnum> Reasons => _reasons;

    public ImportCatalogueSelectionModel InheritedMethod { get; }

    public ImportCatalogueSelectionModel InheritedSpecies { get; }

    public ImportCatalogueSelectionModel? MethodOverride { get; private set; }

    public ImportCatalogueSelectionModel? SpeciesOverride { get; private set; }

    public ImportCatalogueSelectionModel Method => MethodOverride ?? InheritedMethod;

    public ImportCatalogueSelectionModel Species => SpeciesOverride ?? InheritedSpecies;

    public ImportCatchReviewStatusEnum ReviewStatus { get; private set; }

    public bool IsRemoved { get; private set; }

    public bool IsReadyForConfirmation
    {
        get
        {
            return !IsRemoved
                && ReviewStatus == ImportCatchReviewStatusEnum.Reviewed
                && CanBeReviewed;
        }
    }

    public bool CanBeReviewed => !IsRemoved
        && CaughtOn.IsResolved
        && Method.IsValid
        && Species.IsValid
        && IsLocationDecisionResolved
        && _gpsConflictResolved
        && !_canonicalReviewRequired
        && _photoIds.Count > 0;

    public bool CanConfirmDisplayedValues => !IsRemoved
        && CaughtOn.Instant.HasValue
        && Method.IsValid
        && Species.IsValid
        && _gpsConflictResolved
        && _photoIds.Count > 0;

    public bool IsLocationDecisionResolved => Location is null
        || !Location.HistoricalGpsPresent
        || Location.Decision != ImportLocationDecisionEnum.Undecided;

    public bool HasUnresolvedGpsConflict => !_gpsConflictResolved;

    public bool RequiresCanonicalReview => _canonicalReviewRequired;

    public void SetCaughtOn(ImportTimestampModel caughtOn)
    {
        CaughtOn = caughtOn;
        CanonicalValueChanged();
    }

    public void SetLocation(ImportLocationModel? location)
    {
        Location = location;
        _gpsConflictResolved = location is null
            || !location.HistoricalGpsPresent
            || location.Decision != ImportLocationDecisionEnum.Undecided;
        CanonicalValueChanged();
    }

    public void OverrideMethod(ImportCatalogueSelectionModel? method)
    {
        MethodOverride = method;
        CanonicalValueChanged();
    }

    public void OverrideSpecies(ImportCatalogueSelectionModel? species)
    {
        SpeciesOverride = species;
        CanonicalValueChanged();
    }

    public void SetWeight(decimal? weight)
    {
        Weight = weight;
        CanonicalValueChanged();
    }

    public void SetLength(decimal? length)
    {
        Length = length;
        CanonicalValueChanged();
    }

    public void MarkReviewed()
    {
        if (!CanBeReviewed)
        {
            throw new InvalidOperationException("The Catch proposal still requires correction.");
        }

        ReviewStatus = ImportCatchReviewStatusEnum.Reviewed;
    }

    public void ConfirmDisplayedValues()
    {
        if (!CanConfirmDisplayedValues)
        {
            throw new InvalidOperationException("The Catch proposal has no complete displayed values to confirm.");
        }

        if (!CaughtOn.IsResolved)
        {
            var displayedCaughtOn = CaughtOn.Instant!.Value.DateTime;
            CaughtOn = CaughtOn.Confirm(displayedCaughtOn);
        }

        if (Location is { HasCanonicalCoordinates: true, Decision: ImportLocationDecisionEnum.Undecided })
        {
            Location = Location.Accept();
        }

        ConfirmCanonicalValues();
        MarkReviewed();
    }

    public void ConfirmCanonicalValues()
    {
        _canonicalReviewRequired = false;
        ReviewStatus = ImportCatchReviewStatusEnum.Draft;
    }

    public void AddPhoto(Guid photoId)
    {
        if (photoId == Guid.Empty || _photoIds.Contains(photoId))
        {
            throw new InvalidOperationException("The photo is invalid or already belongs to this Catch proposal.");
        }

        _photoIds.Add(photoId);
        ReviewStatus = ImportCatchReviewStatusEnum.Draft;
    }

    public void SetPhotos(IEnumerable<Guid> photoIds)
    {
        var replacements = photoIds.ToArray();
        if (replacements.Length == 0
            || replacements.Any(photoId => photoId == Guid.Empty)
            || replacements.Distinct().Count() != replacements.Length)
        {
            throw new InvalidOperationException("A Catch proposal requires unique valid photos.");
        }

        _photoIds.Clear();
        _photoIds.AddRange(replacements);
        ReviewStatus = ImportCatchReviewStatusEnum.Draft;
    }

    public void RemovePhoto(Guid photoId)
    {
        _photoIds.Remove(photoId);
        ReviewStatus = ImportCatchReviewStatusEnum.Draft;
        if (_photoIds.Count == 0)
        {
            IsRemoved = true;
        }
    }

    public void Remove()
    {
        IsRemoved = true;
    }

    public void RequireCanonicalReview()
    {
        _canonicalReviewRequired = true;
        ReviewStatus = ImportCatchReviewStatusEnum.Draft;
    }

    private void CanonicalValueChanged()
    {
        _canonicalReviewRequired = false;
        ReviewStatus = ImportCatchReviewStatusEnum.Draft;
    }
}
