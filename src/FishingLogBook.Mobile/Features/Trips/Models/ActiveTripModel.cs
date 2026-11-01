using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Trips.Models;

/// <summary>What the active-trip view needs to show a trip: the display text, the timeline and what the viewer may do.</summary>
public sealed record ActiveTripModel(
    LocalTripRecordModel Trip,
    UiTextService Text,
    string DateHeading,
    string TitleText,
    string PlaceText,
    string StartedText,
    string ElapsedText,
    string StatusText,
    string StatusIcon,
    bool IsCompleted,
    string CatchSummary,
    string PhotographSummary,
    string NoteSummary,
    IReadOnlyList<TripTimelineRowModel> Timeline,
    bool CanRecordCatch,
    bool CanAddCatches,
    bool CanAddPhotographs,
    bool CanManageTrip,
    bool ShowBackToLogbook,
    bool ShowPhotographPicker,
    string MessageText)
{
    public bool HasTitle => !string.IsNullOrWhiteSpace(TitleText);

    public bool HasPlace => !string.IsNullOrWhiteSpace(PlaceText);

    public bool HasMessage => !string.IsNullOrWhiteSpace(MessageText);
}
