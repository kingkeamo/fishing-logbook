using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Trips.Components.TripTimeline;

public partial class TripTimeline : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Dictionary<DateTimeOffset, string> _localTimes = [];

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<TripTimelineItemModel> Items { get; set; } = [];

    [Parameter]
    public string CatchBaseHref { get; set; } = "/catches";

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
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

        await RememberLocalTimesAsync(pending);
    }

    private async Task RememberLocalTimesAsync(IReadOnlyList<DateTimeOffset> pending)
    {
        try
        {
            var values = await Task.WhenAll(pending.Select(occurredOn =>
                Time.ToDateTimeLocalValueAsync(occurredOn, _cancellationTokenSource.Token)));
            for (var index = 0; index < pending.Count; index++)
            {
                var value = values[index];
                _localTimes[pending[index]] = value.Length >= 16 ? value[11..16] : value;
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

    private string TimeLabel(TripTimelineItemModel item)
    {
        return _localTimes.TryGetValue(item.OccurredOn, out var local)
            ? local
            : item.OccurredOn.ToString("HH:mm");
    }

    private string KindLabel(TripTimelineKindEnum kind)
    {
        return kind switch
        {
            TripTimelineKindEnum.Started => Loc["Trip_Timeline_Started"],
            TripTimelineKindEnum.Catch => Loc["Trip_Timeline_Catch"],
            TripTimelineKindEnum.Photograph => Loc["Trip_Timeline_Photograph"],
            TripTimelineKindEnum.Note => Loc["Trip_Timeline_Note"],
            _ => Loc["Trip_Timeline_Finished"]
        };
    }

    private string Description(TripTimelineItemModel item)
    {
        return item.Kind switch
        {
            TripTimelineKindEnum.Started => Loc["Trip_Timeline_StartedDescription"],
            TripTimelineKindEnum.Catch => string.IsNullOrWhiteSpace(item.SpeciesName)
                ? Loc["Trip_Timeline_CatchUnknownSpecies"]
                : item.SpeciesName!,
            TripTimelineKindEnum.Photograph => Loc["Trip_Timeline_PhotographDescription"],
            TripTimelineKindEnum.Note => item.Text ?? string.Empty,
            _ => Loc["Trip_Timeline_FinishedDescription"]
        };
    }

    private string? CatchHref(TripTimelineItemModel item)
    {
        return item.Kind == TripTimelineKindEnum.Catch && item.CatchId is { } catchId
            ? $"{CatchBaseHref}?catchId={catchId:D}"
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
        return item.CatchId is { } catchId
            ? $"trip-timeline-{kind}-{catchId:D}"
            : $"trip-timeline-{kind}-{item.OccurredOn.ToUnixTimeMilliseconds()}";
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
