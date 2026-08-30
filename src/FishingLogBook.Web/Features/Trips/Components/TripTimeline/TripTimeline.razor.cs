using System.Globalization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Trips.Components.TripTimeline;

public partial class TripTimeline : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Dictionary<DateTimeOffset, DateTime> _localTimes = [];
    private readonly Dictionary<Guid, string> _mediaSources = [];
    private readonly HashSet<Guid> _mediaRequested = [];

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<TripTimelineItemModel> Items { get; set; } = [];

    [Parameter]
    public Guid ViewerUserId { get; set; }

    [Parameter]
    public Guid TripId { get; set; }

    [Parameter]
    public bool AllowLocalMedia { get; set; } = true;

    [Parameter]
    public bool CanEditNotes { get; set; }

    [Parameter]
    public IReadOnlyList<TripContributorDto> Contributors { get; set; } = [];

    [Parameter]
    public EventCallback<Guid> OnDeleteNote { get; set; }

    [Parameter]
    public EventCallback<TripTimelineItemModel> OnEditNote { get; set; }

    [Parameter]
    public string CatchBaseHref { get; set; } = "/catches";

    [Parameter]
    public WeightUnitEnum WeightUnit { get; set; } = WeightUnitEnum.Kg;

    [Parameter]
    public LengthUnitEnum LengthUnit { get; set; } = LengthUnitEnum.Cm;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private ITripPhotographStore TripPhotographStore { get; set; } = default!;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        await RememberLocalTimesAsync();
        await LoadMediaAsync();
    }

    private async Task RememberLocalTimesAsync()
    {
        var pending = Items
            .Select(item => item.OccurredOn)
            .Where(occurredOn => !_localTimes.ContainsKey(occurredOn))
            .Distinct()
            .ToArray();
        if (pending.Length == 0)
        {
            return;
        }

        try
        {
            var values = await Task.WhenAll(pending.Select(occurredOn =>
                Time.ToDateTimeLocalValueAsync(occurredOn, _cancellationTokenSource.Token)));
            for (var index = 0; index < pending.Length; index++)
            {
                var parsed = ParseLocalValue(values[index]);
                if (parsed is not null)
                {
                    _localTimes[pending[index]] = parsed.Value;
                }
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading a trip timeline time", exception, CancellationToken.None);
        }
    }

    private async Task LoadMediaAsync()
    {
        if (!AllowLocalMedia)
        {
            return;
        }

        foreach (var item in Items.Where(NeedsLocalMedia))
        {
            _mediaRequested.Add(item.PhotographId!.Value);
            await LoadMediaAsync(item);
        }
    }

    private bool NeedsLocalMedia(TripTimelineItemModel item)
    {
        return item.PhotographId is { } photographId
            && item.PhotographUrl is null
            && !_mediaRequested.Contains(photographId)
            && item.Kind is TripTimelineKindEnum.Photograph or TripTimelineKindEnum.Catch;
    }

    private async Task LoadMediaAsync(TripTimelineItemModel item)
    {
        try
        {
            var bytes = item.Kind == TripTimelineKindEnum.Photograph
                ? await TripPhotographStore.GetBytesAsync(
                    ViewerUserId,
                    TripId,
                    item.PhotographId!.Value,
                    _cancellationTokenSource.Token)
                : await CatchStore.GetPhotographBytesAsync(
                    ViewerUserId,
                    item.CatchId ?? Guid.Empty,
                    item.PhotographId!.Value,
                    _cancellationTokenSource.Token);
            if (bytes is { Length: > 0 })
            {
                _mediaSources[item.PhotographId!.Value] =
                    $"data:{item.ContentType};base64,{Convert.ToBase64String(bytes)}";
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading a trip timeline photograph",
                exception,
                CancellationToken.None);
        }
    }

    private string? MediaSource(TripTimelineItemModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.PhotographUrl))
        {
            return item.PhotographUrl;
        }

        return item.PhotographId is { } photographId
            && _mediaSources.TryGetValue(photographId, out var source)
            ? source
            : null;
    }

    private bool ShowDate(int index)
    {
        var current = LocalDate(Items[index]);
        if (current is null)
        {
            return false;
        }

        if (index == 0)
        {
            return Items
                .Select(LocalDate)
                .OfType<DateTime>()
                .Select(value => value.Date)
                .Distinct()
                .Count() > 1;
        }

        var previous = LocalDate(Items[index - 1]);
        return previous is null || previous.Value.Date != current.Value.Date;
    }

    private DateTime? LocalDate(TripTimelineItemModel item)
    {
        return _localTimes.TryGetValue(item.OccurredOn, out var local) ? local : null;
    }

    private string TimeLabel(TripTimelineItemModel item, bool showDate)
    {
        var local = LocalDate(item);
        var time = local is null
            ? item.OccurredOn.ToString("HH:mm", CultureInfo.CurrentCulture)
            : local.Value.ToString("t", CultureInfo.CurrentCulture);
        if (!showDate)
        {
            return time;
        }

        var date = local is null
            ? item.OccurredOn.ToString("d MMM", CultureInfo.CurrentCulture)
            : local.Value.ToString("d MMM", CultureInfo.CurrentCulture);
        return $"{date} · {time}";
    }

    private string SpeciesLabel(TripTimelineItemModel item)
    {
        return string.IsNullOrWhiteSpace(item.SpeciesName)
            ? Loc["Trip_Timeline_CatchUnknownSpecies"]
            : item.SpeciesName!;
    }

    private string? MeasurementsLabel(TripTimelineItemModel item)
    {
        var weight = Measurement.ToDisplayWeight(item.Weight, WeightUnit);
        var length = Measurement.ToDisplayLength(item.Length, LengthUnit);
        var weightText = weight is null
            ? null
            : $"{weight.Value.ToString("0.##", CultureInfo.CurrentCulture)} {WeightUnitLabel}";
        var lengthText = length is null
            ? null
            : $"{length.Value.ToString("0.##", CultureInfo.CurrentCulture)} {LengthUnitLabel}";
        if (weightText is null && lengthText is null)
        {
            return null;
        }

        return string.Join(" · ", new[] { weightText, lengthText }.Where(value => value is not null));
    }

    private string WeightUnitLabel => WeightUnit == WeightUnitEnum.Lb
        ? Loc["Catch_WeightUnitShort_Lb"]
        : Loc["Catch_WeightUnitShort_Kg"];

    private string LengthUnitLabel => LengthUnit == LengthUnitEnum.In
        ? Loc["Catch_LengthUnitShort_In"]
        : Loc["Catch_LengthUnitShort_Cm"];

    private string Description(TripTimelineItemModel item)
    {
        return item.Kind == TripTimelineKindEnum.Started
            ? Loc["Trip_Timeline_StartedDescription"]
            : Loc["Trip_Timeline_FinishedDescription"];
    }

    private string? CatchHref(TripTimelineItemModel item)
    {
        return item.CatchId is { } catchId ? $"{CatchBaseHref}/{catchId:D}/edit" : null;
    }

    private static DateTime? ParseLocalValue(string value)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-ddTHH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static string IconFor(TripTimelineKindEnum kind)
    {
        return kind switch
        {
            TripTimelineKindEnum.Started => Icons.Material.Filled.PlayArrow,
            TripTimelineKindEnum.Catch => Icons.Material.Filled.Grade,
            TripTimelineKindEnum.Photograph => Icons.Material.Filled.PhotoCamera,
            TripTimelineKindEnum.Note => Icons.Material.Filled.Notes,
            _ => Icons.Material.Filled.Flag
        };
    }

    private static Color ColourFor(TripTimelineKindEnum kind)
    {
        return kind switch
        {
            TripTimelineKindEnum.Catch => Color.Primary,
            TripTimelineKindEnum.Started or TripTimelineKindEnum.Finished => Color.Secondary,
            _ => Color.Default
        };
    }

    private static string ItemId(TripTimelineItemModel item)
    {
        var kind = item.Kind.ToString().ToLowerInvariant();
        var identity = item.CatchId ?? item.NoteId ?? item.PhotographId;
        return identity is { } value
            ? $"trip-timeline-{kind}-{value:D}"
            : $"trip-timeline-{kind}-{item.OccurredOn.ToUnixTimeMilliseconds()}";
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private bool IsOwnContribution(TripTimelineItemModel item)
    {
        return item.ContributedByUserId == Guid.Empty || item.ContributedByUserId == ViewerUserId;
    }

    private string? ContributorName(TripTimelineItemModel item)
    {
        if (item.Kind == TripTimelineKindEnum.Catch)
        {
            return AnglerName(item);
        }

        if (IsOwnContribution(item))
        {
            return null;
        }

        return DisplayNameFor(item.ContributedByUserId);
    }

    private string AnglerName(TripTimelineItemModel item)
    {
        return DisplayNameFor(item.ContributedByUserId);
    }

    private string? RecordedByName(TripTimelineItemModel item)
    {
        return item.RecordedByUserId == Guid.Empty || item.RecordedByUserId == item.ContributedByUserId
            ? null
            : DisplayNameFor(item.RecordedByUserId);
    }

    private string DisplayNameFor(Guid userId)
    {
        var contributor = Contributors.FirstOrDefault(candidate => candidate.UserId == userId);
        return string.IsNullOrWhiteSpace(contributor?.DisplayName)
            ? Loc["Trip_ContributorUnknown"].Value
            : contributor.DisplayName;
    }
}
