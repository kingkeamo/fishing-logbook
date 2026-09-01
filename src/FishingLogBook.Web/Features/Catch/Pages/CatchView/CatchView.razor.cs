using System.Globalization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.CatchView;

public partial class CatchView : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private CatchViewDto? _catch;
    private string? _caughtOnLabel;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _isLoading = true;
    private bool _loadFailed;

    [Parameter]
    public Guid CatchId { get; set; }

    [Inject]
    private ICatchClient CatchClient { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IReadOnlyList<PhotographCarouselItemModel> CarouselPhotographs =>
        _catch is null
            ? []
            : _catch.Photographs
                .Select(photograph => new PhotographCarouselItemModel(
                    photograph.Id,
                    photograph.ContentType,
                    RemoteUrl: photograph.Url))
                .ToArray();

    private string SpeciesLabel => string.IsNullOrWhiteSpace(_catch?.SpeciesName)
        ? Loc["Catch_UnknownSpecies"]
        : _catch.SpeciesName;

    private string CaughtOnLabel => _caughtOnLabel
        ?? _catch?.CaughtOn.ToString("g", CultureInfo.CurrentCulture)
        ?? string.Empty;

    private string? WeightLabel
    {
        get
        {
            var weight = Measurement.ToDisplayWeight(_catch?.Weight, _weightUnit);
            return weight is null
                ? null
                : $"{weight.Value.ToString("0.##", CultureInfo.CurrentCulture)} {WeightUnitLabel}";
        }
    }

    private string? LengthLabel
    {
        get
        {
            var length = Measurement.ToDisplayLength(_catch?.Length, _lengthUnit);
            return length is null
                ? null
                : $"{length.Value.ToString("0.##", CultureInfo.CurrentCulture)} {LengthUnitLabel}";
        }
    }

    private string WeightUnitLabel => _weightUnit == WeightUnitEnum.Lb
        ? Loc["Catch_WeightUnitShort_Lb"]
        : Loc["Catch_WeightUnitShort_Kg"];

    private string LengthUnitLabel => _lengthUnit == LengthUnitEnum.In
        ? Loc["Catch_LengthUnitShort_In"]
        : Loc["Catch_LengthUnitShort_Cm"];

    private bool HasMethod => !string.IsNullOrWhiteSpace(_catch?.Method);

    private bool HasBaitOrLure => !string.IsNullOrWhiteSpace(_catch?.BaitOrLure);

    private bool HasNotes => !string.IsNullOrWhiteSpace(_catch?.Notes);

    private bool HasAnglerName => !string.IsNullOrWhiteSpace(_catch?.AnglerName);

    private bool HasRecordedByName =>
        !string.IsNullOrWhiteSpace(_catch?.RecordedByName)
        && _catch.RecordedByUserId != _catch.AnglerUserId;

    private string? LocationLabel
    {
        get
        {
            var location = _catch?.Location;
            if (location is null)
            {
                return null;
            }

            if (location.Latitude is { } latitude && location.Longitude is { } longitude)
            {
                return FormatCoordinates(latitude, longitude);
            }

            if (location.ApproximateLatitude is { } approximateLatitude
                && location.ApproximateLongitude is { } approximateLongitude)
            {
                return FormatCoordinates(approximateLatitude, approximateLongitude);
            }

            return string.IsNullOrWhiteSpace(location.FishingVenueName)
                ? null
                : location.FishingVenueName;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    private async Task RetryLoadAsync()
    {
        if (_isLoading)
        {
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        _catch = null;
        _caughtOnLabel = null;
        try
        {
            var catchRecord = await CatchClient.GetAsync(CatchId, _cancellationTokenSource.Token);
            if (catchRecord is null)
            {
                _loadFailed = true;
                return;
            }

            _catch = catchRecord;
            await LoadPreferencesAsync();
            await RememberCaughtOnAsync(catchRecord.CaughtOn);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading a catch", exception, CancellationToken.None);
            _loadFailed = true;
            _catch = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadPreferencesAsync()
    {
        try
        {
            var preferences = await AnglerPreferences.GetAsync(_cancellationTokenSource.Token);
            ApplyPreferences(preferences);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading catch view preferences", exception, CancellationToken.None);
            ApplyPreferences(AnglerPreferencesModel.Empty);
        }
    }

    private void ApplyPreferences(AnglerPreferencesModel preferences)
    {
        _weightUnit = preferences.WeightUnit;
        _lengthUnit = preferences.LengthUnit;
    }

    private async Task RememberCaughtOnAsync(DateTimeOffset caughtOn)
    {
        try
        {
            var localValue = await Time.ToDateTimeLocalValueAsync(caughtOn, _cancellationTokenSource.Token);
            _caughtOnLabel = DateTime.TryParseExact(
                localValue,
                "yyyy-MM-ddTHH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? parsed.ToString("g", CultureInfo.CurrentCulture)
                : caughtOn.ToString("g", CultureInfo.CurrentCulture);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading a catch view time", exception, CancellationToken.None);
            _caughtOnLabel = caughtOn.ToString("g", CultureInfo.CurrentCulture);
        }
    }

    private static string FormatCoordinates(double latitude, double longitude)
    {
        return $"{latitude.ToString("0.#####", CultureInfo.CurrentCulture)}, {longitude.ToString("0.#####", CultureInfo.CurrentCulture)}";
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
