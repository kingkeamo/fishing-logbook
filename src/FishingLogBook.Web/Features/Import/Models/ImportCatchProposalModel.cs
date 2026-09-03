using FishingLogBook.Web.Features.Import.Enums;

namespace FishingLogBook.Web.Features.Import.Models;

public sealed class ImportCatchProposalModel
{
    private readonly List<Guid> _photoIds;

    public ImportCatchProposalModel(
        Guid id,
        IEnumerable<Guid> photoIds,
        ImportTimestampModel caughtOn,
        ImportCatalogueSelectionModel inheritedMethod,
        ImportCatalogueSelectionModel inheritedSpecies,
        ImportLocationModel? location = null)
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
    }

    public Guid Id { get; }

    public IReadOnlyList<Guid> PhotoIds => _photoIds;

    public ImportTimestampModel CaughtOn { get; private set; }

    public ImportLocationModel? Location { get; private set; }

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
                && CaughtOn.IsResolved
                && Method.IsValid
                && Species.IsValid
                && _photoIds.Count > 0;
        }
    }

    public void SetCaughtOn(ImportTimestampModel caughtOn)
    {
        CaughtOn = caughtOn;
    }

    public void SetLocation(ImportLocationModel? location)
    {
        Location = location;
    }

    public void OverrideMethod(ImportCatalogueSelectionModel? method)
    {
        MethodOverride = method;
    }

    public void OverrideSpecies(ImportCatalogueSelectionModel? species)
    {
        SpeciesOverride = species;
    }

    public void MarkReviewed()
    {
        ReviewStatus = ImportCatchReviewStatusEnum.Reviewed;
    }

    public void AddPhoto(Guid photoId)
    {
        if (photoId == Guid.Empty || _photoIds.Contains(photoId))
        {
            throw new InvalidOperationException("The photo is invalid or already belongs to this Catch proposal.");
        }

        _photoIds.Add(photoId);
    }

    public void RemovePhoto(Guid photoId)
    {
        _photoIds.Remove(photoId);
        if (_photoIds.Count == 0)
        {
            IsRemoved = true;
        }
    }

    public void Remove()
    {
        IsRemoved = true;
    }
}
