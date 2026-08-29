using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TripCatchesComponent = FishingLogBook.Web.Features.Trips.Components.TripCatches.TripCatches;
using TripNotesComponent = FishingLogBook.Web.Features.Trips.Components.TripNotes.TripNotes;

namespace FishingLogBook.Web.Features.Trips.Components.ActiveTripView;

public partial class ActiveTripView : ComponentBase
{
    private TripNotesComponent? _notes;
    private TripCatchesComponent? _catches;
    private bool _showPhotographPicker;

    [Parameter]
    [EditorRequired]
    public TripModel Trip { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public Guid ViewerUserId { get; set; }

    [Parameter]
    public IReadOnlyList<TripContributorDto> Contributors { get; set; } = [];

    [Parameter]
    public bool ShowParticipants { get; set; }

    [Parameter]
    public EventCallback OnShowParticipants { get; set; }

    [Parameter]
    public string? StartedLabel { get; set; }

    [Parameter]
    public string? ElapsedLabel { get; set; }

    [Parameter]
    public string? GeneratedTitle { get; set; }

    [Parameter]
    public bool IsFinishing { get; set; }

    [Parameter]
    public EventCallback OnFinish { get; set; }

    [Parameter]
    public EventCallback OnContentChanged { get; set; }

    [Parameter]
    public string RecordCatchBaseHref { get; set; } = "/catches/record";

    [Parameter]
    public string TripBaseHref { get; set; } = "/trips";

    [Parameter]
    public string LogbookHref { get; set; } = "/catches";

    [Parameter]
    public string CatchBaseHref { get; set; } = "/catches";

    [Parameter]
    public int? CatchCount { get; set; }

    [Parameter]
    public int? PhotographCount { get; set; }

    [Parameter]
    public int? NoteCount { get; set; }

    [Parameter]
    public IReadOnlyList<TripTimelineItemModel> Timeline { get; set; } = [];

    [Parameter]
    public bool AllowLocalMedia { get; set; } = true;

    [Parameter]
    public bool CanEdit { get; set; } = true;

    [Parameter]
    public bool CanAddNotes { get; set; } = true;

    [Parameter]
    public TripStorageEnum NoteStorage { get; set; } = TripStorageEnum.LocalFirst;

    [Parameter]
    public bool CanAddCatches { get; set; } = true;

    [Parameter]
    public WeightUnitEnum WeightUnit { get; set; } = WeightUnitEnum.Kg;

    [Parameter]
    public LengthUnitEnum LengthUnit { get; set; } = LengthUnitEnum.Cm;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool IsCompleted => Trip.Status == TripConstants.Completed;

    private string RecordCatchHref => $"{RecordCatchBaseHref}?tripId={Trip.Id:D}";

    private string EditHref => $"{TripBaseHref}/{Trip.Id:D}/edit";

    private string CatchSummary
    {
        get
        {
            return CatchCount switch
            {
                null or 0 => Loc["Trip_CatchesNone"],
                1 => Loc["Trip_CatchesOne"],
                _ => string.Format(Loc["Trip_CatchesMany"], CatchCount)
            };
        }
    }

    private string PhotographSummary
    {
        get
        {
            return PhotographCount switch
            {
                null or 0 => Loc["Trip_ListPhotographsNone"],
                1 => Loc["Trip_ListPhotographsOne"],
                _ => string.Format(Loc["Trip_ListPhotographsMany"], PhotographCount)
            };
        }
    }

    private string NoteSummary
    {
        get
        {
            return NoteCount switch
            {
                null or 0 => Loc["Trip_ListNotesNone"],
                1 => Loc["Trip_ListNotesOne"],
                _ => string.Format(Loc["Trip_ListNotesMany"], NoteCount)
            };
        }
    }

    private bool HasPlace => !string.IsNullOrWhiteSpace(Trip.PlaceName);

    private string DateHeading
    {
        get
        {
            return string.IsNullOrWhiteSpace(GeneratedTitle)
                ? Loc["Trip_ActiveLabel"].Value
                : GeneratedTitle!;
        }
    }

    private bool HasTitle => !string.IsNullOrWhiteSpace(Trip.Title);

    private void TogglePhotographPicker()
    {
        _showPhotographPicker = !_showPhotographPicker;
    }

    private async Task AddCatchesAsync()
    {
        if (_catches is null)
        {
            return;
        }

        _showPhotographPicker = false;
        await _catches.AddCatchesAsync();
    }

    private async Task EditNoteAsync(TripTimelineItemModel item)
    {
        if (_notes is null || item.NoteId is not { } noteId)
        {
            return;
        }

        await _notes.EditNoteAsync(noteId, item.Text ?? string.Empty, item.OccurredOn);
    }

    private async Task DeleteNoteAsync(Guid noteId)
    {
        if (_notes is null)
        {
            return;
        }

        await _notes.RemoveNoteAsync(noteId);
    }
}
