using System.Globalization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Dashboard.Components.PersonalBestTable;

public partial class PersonalBestTable : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
    [Inject] private IMeasurementService Measurement { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ITimeService Time { get; set; } = default!;
    [Parameter] public string Id { get; set; } = string.Empty;
    [Parameter] public IReadOnlyList<DashboardPersonalBestDto> Rows { get; set; } = [];
    [Parameter] public WeightUnitEnum WeightUnit { get; set; }
    [Parameter] public bool IsLoaded { get; set; }
    [Parameter] public string EmptyText { get; set; } = string.Empty;
    private IReadOnlyDictionary<Guid, string> _dates = new Dictionary<Guid, string>();
    private const string DateTimeLocalFormat = "yyyy-MM-dd'T'HH:mm";
    private long _request;

    protected override async Task OnParametersSetAsync()
    {
        var request = ++_request;
        var token = _lifetime.Token;
        var dates = new Dictionary<Guid, string>();
        foreach (var row in Rows)
        {
            if (row.CaughtOn is { } caughtOn)
            {
                string local;
                try
                {
                    local = await Time.ToDateTimeLocalValueAsync(caughtOn, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (JSException)
                {
                    continue;
                }

                if (DateTime.TryParseExact(local, DateTimeLocalFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    dates[row.CatchId] = parsed.ToString("d", CultureInfo.CurrentCulture);
                }
            }
        }
        if (request == _request && !token.IsCancellationRequested)
        {
            _dates = dates;
        }
    }

    private string CaughtOn(DashboardPersonalBestDto row)
    {
        return _dates.GetValueOrDefault(row.CatchId) ?? Loc["Catch_MeasurementNotRecorded"];
    }

    public void Dispose()
    {
        ++_request;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private string Species(DashboardPersonalBestDto row)
    {
        return row.SpeciesName ?? Loc["DashboardUnknownSpecies"];
    }

    private void OpenCatch(Guid catchId)
    {
        Navigation.NavigateTo($"/catches/{catchId}");
    }

    private string Method(DashboardPersonalBestDto row)
    {
        return row.Method ?? Loc["DashboardUnknownMethod"];
    }

    private string Weight(DashboardPersonalBestDto row)
    {
        return Measurement.FormatWeight(row.Weight, WeightUnit,
            Loc[WeightUnit == WeightUnitEnum.Lb ? "Catch_WeightUnitShort_Lb" : "Catch_WeightUnitShort_Kg"],
            Loc["Catch_WeightUnitShort_Oz"]);
    }
}
