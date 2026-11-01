using FishingLogBook.Mobile.Features.Trips.Enums;

namespace FishingLogBook.Mobile.Features.Trips.Models;

public sealed record TripTimelineRowModel(
    TripTimelineKindEnum Kind,
    string TimeText,
    string? ContributorText,
    string Title,
    string? Detail,
    string? PhotoSource,
    Guid? CatchId,
    Guid? NoteId,
    bool CanDelete,
    string? PendingText)
{
    public string? RecordedByText { get; init; }

    public bool CanEditCatch { get; init; }

    public bool IsLast { get; init; }

    public bool HasContributor => !string.IsNullOrWhiteSpace(ContributorText);

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool HasRecordedBy => !string.IsNullOrWhiteSpace(RecordedByText);

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoSource);

    public bool IsPending => !string.IsNullOrWhiteSpace(PendingText);

    public bool IsCatch => Kind == TripTimelineKindEnum.Catch;

    public bool IsNote => Kind == TripTimelineKindEnum.Note;

    public bool IsPhotograph => Kind == TripTimelineKindEnum.Photograph;

    public bool IsMilestone => Kind is TripTimelineKindEnum.Started or TripTimelineKindEnum.Finished;

    public bool ShowPhotograph => IsPhotograph && HasPhoto;

    public bool ShowDescription => IsMilestone || (IsPhotograph && !HasPhoto);

    public bool CanOpenCatch => IsCatch && CatchId is not null && CanEditCatch;

    public bool NotLast => !IsLast;

    public string MarkerIcon => Kind switch
    {
        TripTimelineKindEnum.Started => "icon_play.png",
        TripTimelineKindEnum.Catch => "icon_star.png",
        TripTimelineKindEnum.Photograph => "icon_camera.png",
        TripTimelineKindEnum.Note => "icon_note.png",
        _ => "icon_flag.png"
    };
}
