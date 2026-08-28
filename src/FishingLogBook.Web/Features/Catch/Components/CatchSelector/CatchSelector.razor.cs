using System.Globalization;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Components.CatchSelector;

public partial class CatchSelector : ComponentBase, IDisposable
{
    private const int LocalValueLength = 16;
    private const int TimePartIndex = 11;
    private const int TimePartLength = 5;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly HashSet<Guid> _selected = [];
    private readonly Dictionary<Guid, string> _mediaSources = [];
    private readonly HashSet<Guid> _mediaRequested = [];
    private readonly Dictionary<Guid, string> _localTimes = [];

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<CatchModel> Catches { get; set; } = [];

    [Parameter]
    public string UnknownSpeciesLabel { get; set; } = string.Empty;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public Guid OwnerUserId { get; set; }

    [Parameter]
    public WeightUnitEnum WeightUnit { get; set; } = WeightUnitEnum.Kg;

    [Parameter]
    public LengthUnitEnum LengthUnit { get; set; } = LengthUnitEnum.Cm;

    [Parameter]
    public EventCallback<IReadOnlyList<Guid>> SelectedChanged { get; set; }

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        var available = Catches.Select(candidate => candidate.Id).ToHashSet();
        if (_selected.RemoveWhere(id => !available.Contains(id)) > 0)
        {
            await PublishSelectionAsync();
        }

        await RememberLocalTimesAsync();
        await LoadThumbnailsAsync();
    }

    private async Task RememberLocalTimesAsync()
    {
        var pending = Catches
            .Where(candidate => !_localTimes.ContainsKey(candidate.Id))
            .ToArray();
        if (pending.Length == 0)
        {
            return;
        }

        try
        {
            var values = await Task.WhenAll(pending.Select(candidate =>
                Time.ToDateTimeLocalValueAsync(candidate.CaughtOn, _cancellationTokenSource.Token)));
            for (var index = 0; index < pending.Length; index++)
            {
                var value = values[index];
                _localTimes[pending[index].Id] = value.Length >= LocalValueLength
                    ? value.Substring(TimePartIndex, TimePartLength)
                    : value;
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading a catch selector time", exception, CancellationToken.None);
        }
    }

    private async Task LoadThumbnailsAsync()
    {
        foreach (var candidate in Catches)
        {
            var photograph = candidate.Photographs.Count > 0 ? candidate.Photographs[0] : null;
            if (photograph is null || !_mediaRequested.Add(photograph.Id))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(photograph.RemoteUrl))
            {
                _mediaSources[photograph.Id] = photograph.RemoteUrl!;
                continue;
            }

            await LoadThumbnailAsync(candidate.Id, photograph);
        }
    }

    private async Task LoadThumbnailAsync(Guid catchId, CatchPhotographModel photograph)
    {
        try
        {
            var bytes = await CatchStore.GetPhotographBytesAsync(
                OwnerUserId,
                catchId,
                photograph.Id,
                _cancellationTokenSource.Token);
            if (bytes is { Length: > 0 })
            {
                _mediaSources[photograph.Id] =
                    $"data:{photograph.ContentType};base64,{Convert.ToBase64String(bytes)}";
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading a catch selector thumbnail", exception, CancellationToken.None);
        }
    }

    private string? MediaSource(CatchModel candidate)
    {
        var photograph = candidate.Photographs.Count > 0 ? candidate.Photographs[0] : null;
        return photograph is not null && _mediaSources.TryGetValue(photograph.Id, out var source)
            ? source
            : null;
    }

    private bool IsSelected(Guid catchId)
    {
        return _selected.Contains(catchId);
    }

    private async Task ToggleAsync(Guid catchId, bool selected)
    {
        if (selected)
        {
            _selected.Add(catchId);
        }
        else
        {
            _selected.Remove(catchId);
        }

        await PublishSelectionAsync();
    }

    private async Task PublishSelectionAsync()
    {
        if (!SelectedChanged.HasDelegate)
        {
            return;
        }

        await SelectedChanged.InvokeAsync(
        [
            .. Catches
                .Where(candidate => _selected.Contains(candidate.Id))
                .Select(candidate => candidate.Id)
        ]);
    }

    private string SpeciesLabel(CatchModel candidate)
    {
        return string.IsNullOrWhiteSpace(candidate.SpeciesName)
            ? UnknownSpeciesLabel
            : candidate.SpeciesName!;
    }

    private string Facts(CatchModel candidate)
    {
        var parts = new List<string> { LocalTime(candidate) };
        var weight = Measurement.ToDisplayWeight(candidate.Weight, WeightUnit);
        if (weight is not null)
        {
            parts.Add($"{weight.Value.ToString("0.##", CultureInfo.CurrentCulture)} {WeightUnitLabel}");
        }

        var length = Measurement.ToDisplayLength(candidate.Length, LengthUnit);
        if (length is not null)
        {
            parts.Add($"{length.Value.ToString("0.##", CultureInfo.CurrentCulture)} {LengthUnitLabel}");
        }

        return string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private string LocalTime(CatchModel candidate)
    {
        return _localTimes.TryGetValue(candidate.Id, out var value)
            ? value
            : candidate.CaughtOn.ToString("HH:mm", CultureInfo.CurrentCulture);
    }

    private static string? MethodLabel(CatchModel candidate)
    {
        return string.IsNullOrWhiteSpace(candidate.Method) ? null : candidate.Method;
    }

    private string WeightUnitLabel => WeightUnit == WeightUnitEnum.Lb
        ? Loc["Catch_WeightUnitShort_Lb"]
        : Loc["Catch_WeightUnitShort_Kg"];

    private string LengthUnitLabel => LengthUnit == LengthUnitEnum.In
        ? Loc["Catch_LengthUnitShort_In"]
        : Loc["Catch_LengthUnitShort_Cm"];

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
