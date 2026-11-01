using FishingLogBook.Mobile.Features.Import.Enums;

namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed class ImportCatchRowModel
{
    public ImportCatchRowModel(
        ImportCatchProposalModel proposal,
        int number,
        string title,
        string statusText,
        string timestampText,
        string locationText,
        string methodName,
        string speciesName,
        IReadOnlyList<ImportCatchPhotoModel> photos,
        IReadOnlyList<ImportLocationChoiceModel> locations,
        IReadOnlyList<ImportMergeTargetModel> mergeTargets,
        IReadOnlyList<ImportOptionModel> methodOptions,
        IReadOnlyList<ImportOptionModel> speciesOptions,
        DateTime caughtOnDate,
        TimeSpan caughtOnTime,
        string weightText,
        string lengthText)
    {
        Proposal = proposal;
        Number = number;
        Title = title;
        StatusText = statusText;
        TimestampText = timestampText;
        LocationText = locationText;
        MethodName = methodName;
        SpeciesName = speciesName;
        Photos = photos;
        Locations = locations;
        MergeTargets = mergeTargets;
        MethodOptions = methodOptions;
        SpeciesOptions = speciesOptions;
        CaughtOnDate = caughtOnDate;
        CaughtOnTime = caughtOnTime;
        WeightText = weightText;
        LengthText = lengthText;
        CanConfirm = proposal.CanOfferDisplayedValueConfirmation;
        NeedsCanonicalReview = proposal.RequiresCanonicalReview;
        CanSplit = photos.Count(photo => photo.IsSelected) > 0
            && photos.Count(photo => photo.IsSelected) < photos.Count;
        CanRemoveSelected = photos.Any(photo => photo.IsSelected);
    }

    public ImportCatchProposalModel Proposal { get; }

    public int Number { get; }

    public string Title { get; }

    public string StatusText { get; }

    public string TimestampText { get; }

    public string LocationText { get; }

    public string MethodName { get; }

    public string SpeciesName { get; }

    public IReadOnlyList<ImportCatchPhotoModel> Photos { get; }

    public IReadOnlyList<ImportLocationChoiceModel> Locations { get; }

    public IReadOnlyList<ImportMergeTargetModel> MergeTargets { get; }

    public IReadOnlyList<ImportOptionModel> MethodOptions { get; }

    public IReadOnlyList<ImportOptionModel> SpeciesOptions { get; }

    public DateTime CaughtOnDate { get; set; }

    public TimeSpan CaughtOnTime { get; set; }

    public string WeightText { get; }

    public string LengthText { get; }

    public bool CanConfirm { get; }

    public bool NeedsCanonicalReview { get; }

    public bool CanSplit { get; }

    public bool CanRemoveSelected { get; }

    public bool HasLocationChoices => Locations.Count > 0;

    public bool HasMergeTargets => MergeTargets.Count > 0;

    public bool CanEditPlaceName => !string.IsNullOrWhiteSpace(Proposal.Location?.PlaceName)
        || Proposal.Location?.LookupResult?.Components.Count > 0;

    public bool CanRemoveLocation => Proposal.Location?.HasCanonicalCoordinates == true
        && Proposal.Location.Decision != ImportLocationDecisionEnum.Removed;

    public DateTime CaughtOnLocal => CaughtOnDate.Date + CaughtOnTime;
}
