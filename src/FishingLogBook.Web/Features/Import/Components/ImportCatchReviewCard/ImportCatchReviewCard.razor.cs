using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Import.Components.ImportCatchReviewCard;

public partial class ImportCatchReviewCard : ComponentBase
{
    [Parameter, EditorRequired]
    public ImportCatchProposalModel Proposal { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyList<ImportSelectedPhotoModel> Photos { get; set; } = [];

    [Parameter]
    public int Number { get; set; }

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IReadOnlyList<ImportSelectedPhotoModel> ProposalPhotos
    {
        get
        {
            var byId = Photos.ToDictionary(photo => photo.Id);
            return Proposal.PhotoIds.Where(byId.ContainsKey).Select(photoId => byId[photoId]).ToArray();
        }
    }

    private bool RequiresCorrection
    {
        get
        {
            return !Proposal.CaughtOn.IsResolved
                || Proposal.Reasons.Any(reason => reason != ImportCatchProposalReasonEnum.TrustworthyCaptureTime);
        }
    }

    private string TimestampLabel
    {
        get
        {
            return Proposal.CaughtOn.State switch
            {
                ImportTimestampStateEnum.ExplicitInstant =>
                    Proposal.CaughtOn.Instant!.Value.ToString("dd MMM yyyy · HH:mm zzz"),
                ImportTimestampStateEnum.UserConfirmed =>
                    Proposal.CaughtOn.Instant!.Value.ToString("dd MMM yyyy · HH:mm zzz"),
                ImportTimestampStateEnum.LocalWallClock => Loc["Import_TimestampAmbiguous",
                    Proposal.CaughtOn.LocalWallClock!.Value.ToString("dd MMM yyyy · HH:mm")],
                ImportTimestampStateEnum.WeakFallback => Loc["Import_TimestampWeak",
                    Proposal.CaughtOn.Instant!.Value.ToString("dd MMM yyyy · HH:mm zzz")],
                ImportTimestampStateEnum.Unusable => Loc["Import_TimestampUnusable"],
                _ => Loc["Import_TimestampMissing"]
            };
        }
    }

    private string LocationLabel
    {
        get
        {
            if (Proposal.Reasons.Contains(ImportCatchProposalReasonEnum.ConflictingGps))
            {
                return Loc["Import_LocationNeedsReview"];
            }

            return Proposal.Location?.HasCanonicalCoordinates == true
                ? Loc["Import_LocationAvailable"]
                : Loc["Import_LocationUnavailable"];
        }
    }
}
