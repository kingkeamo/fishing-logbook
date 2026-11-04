using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Mappers;
using FishingLogBook.Mobile.Features.Dashboard.Clients;
using FishingLogBook.Mobile.Features.Dashboard.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Shared.Dashboard.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Dashboard.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IDashboardClient _client;
    private readonly IAccountSessionCoordinator _session;
    private readonly IConnectivityService _connectivity;
    private readonly IAccountUnitsService _units;
    private readonly IMeasurementService _measurement;
    private readonly ICatchRemoteClient _catches;
    private readonly CatchPresentationMapper _catchMapper;
    private readonly NavigationService _navigation;
    private readonly TimeProvider _clock;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private AccountSessionLeaseModel? _owner;
    private CancellationTokenRegistration _accountCancellation;
    private MeasurementUnitsModel _measurementUnits = MeasurementUnitsModel.Default;
    private long _request;
    private string? _statusKey;
    private bool _openingCatch;

    public DashboardViewModel(
        IDashboardClient client, IAccountSessionCoordinator session, IConnectivityService connectivity,
        IAccountUnitsService units, IMeasurementService measurement, ICatchRemoteClient catches,
        CatchPresentationMapper catchMapper, NavigationService navigation, TimeProvider clock,
        UiTextService text, ILogger<DashboardViewModel> logger)
    {
        _client = client;
        _session = session;
        _connectivity = connectivity;
        _units = units;
        _measurement = measurement;
        _catches = catches;
        _catchMapper = catchMapper;
        _navigation = navigation;
        _clock = clock;
        _logger = logger;
        Text = text;
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        FromDate = new DateTime(today.Year, 1, 1);
        ToDate = new DateTime(today.Year, 12, 31);
        Text.PropertyChanged += OnCultureChanged;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public UiTextService Text { get; }
    public DashboardDto? Data { get; private set; }
    public bool IsLoading { get; private set; }
    public bool CanApply => !IsLoading;
    public bool HasData => Data is not null;
    public bool HasStatus => _statusKey is not null;
    public string StatusText => _statusKey is null ? string.Empty : Text[_statusKey];
    public string CatchesTotal => Data?.Lifetime.Catches.ToString(Text.Culture) ?? string.Empty;
    public string TripsTotal => Data?.Lifetime.Trips.ToString(Text.Culture) ?? string.Empty;
    public string SpeciesTotal => Data?.Lifetime.Species.ToString(Text.Culture) ?? string.Empty;
    public bool ShowImportSuggestion => HasData && Data!.Lifetime.Catches == 0;
    public bool ShowLifetimeEmpty => HasData && LifetimeBests.Count == 0;
    public bool ShowStatisticsEmpty => HasData && Statistics.Count == 0;
    public bool ShowFilteredEmpty => HasData && FilteredBests.Count == 0;
    public string LifetimeEmptyTitle => Text[Data?.Lifetime.Catches == 0 ? "DashboardFirstBest" : "DashboardWeightNeeded"];
    public string StatisticsEmptyText => Text[Data?.Lifetime.Catches == 0 ? "DashboardNoCatches" : "DashboardNoMatches"];
    public string FilteredEmptyText => Statistics.Count == 0 ? StatisticsEmptyText : Text["DashboardWeightNeeded"];
    public IReadOnlyList<DashboardRowModel> LifetimeBests { get; private set; } = [];
    public IReadOnlyList<DashboardRowModel> Statistics { get; private set; } = [];
    public IReadOnlyList<DashboardRowModel> FilteredBests { get; private set; } = [];
    public IReadOnlyList<DashboardChoiceModel> SpeciesOptions { get; private set; } = [];
    public IReadOnlyList<DashboardChoiceModel> MethodOptions { get; private set; } = [];
    public IReadOnlyList<DashboardChoiceModel> VenueOptions { get; private set; } = [];
    public DashboardChoiceModel? SelectedSpecies { get; set; }
    public DashboardChoiceModel? SelectedMethod { get; set; }
    public DashboardChoiceModel? SelectedVenue { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PresetIndex { get; private set; } = (int)DashboardDatePresetEnum.ThisYear;
    public bool IsCustom => PresetIndex == (int)DashboardDatePresetEnum.Custom;
    public IReadOnlyList<string> DatePresets => new[]
    {
        Text["DashboardThisMonth"], Text["DashboardThisYear"], Text["DashboardLastYear"],
        Text["DashboardAllTime"], Text["DashboardCustom"]
    };

    public void SelectPreset(int index)
    {
        if (index == PresetIndex || !Enum.IsDefined((DashboardDatePresetEnum)index))
        {
            return;
        }

        PresetIndex = index;
        Notify();
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        PresetIndex = (int)DashboardDatePresetEnum.ThisYear;
        SelectedSpecies = null;
        SelectedMethod = null;
        SelectedVenue = null;
        await LoadAsync(cancellationToken);
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var request = Interlocked.Increment(ref _request);
        var lease = _session.Capture();
        if (_owner is not null && !_session.IsCurrent(_owner))
        {
            Data = null;
        }

        _owner = lease;
        _accountCancellation.Dispose();
        if (lease is not null)
        {
            _accountCancellation = lease.CancellationToken.Register(() => ClearAccountData(lease));
        }

        _statusKey = null;
        if (lease is null || !_connectivity.IsOnline)
        {
            SetFailure("DashboardOffline");
            return;
        }

        var range = DashboardDateRangeModel.Resolve((DashboardDatePresetEnum)PresetIndex,
            DateOnly.FromDateTime(_clock.GetLocalNow().DateTime), DateOnly.FromDateTime(FromDate), DateOnly.FromDateTime(ToDate));
        if (range.From > range.To || range.To == DateOnly.MaxValue)
        {
            _statusKey = "DashboardInvalidDates";
            Notify();
            return;
        }

        IsLoading = true;
        Notify();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.CancellationToken);
        await ReadAsync(range, lease, request, linked.Token);
    }

    private async Task ReadAsync(
        DashboardDateRangeModel range, AccountSessionLeaseModel lease, long request, CancellationToken cancellationToken)
    {
        try
        {
            var filter = BuildFilter(range);
            var result = await _client.GetAsync(filter, cancellationToken);
            if (!IsCurrent(request, lease))
            {
                return;
            }

            if (!result.IsSuccess)
            {
                SetFailure("DashboardError");
                return;
            }

            var units = await _units.GetAsync(cancellationToken);
            if (!IsCurrent(request, lease))
            {
                return;
            }

            _measurementUnits = units;
            Data = result.Value;
            RebuildRows();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (request == _request)
            {
                Data = null;
                RebuildRows();
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Dashboard load failed with {ErrorType}.", exception.GetType().Name);
            if (IsCurrent(request, lease))
            {
                SetFailure("DashboardError");
            }
        }
        finally
        {
            if (request == _request)
            {
                IsLoading = false;
                Notify();
            }
        }
    }

    public async Task OpenCatchAsync(Guid catchId, CancellationToken cancellationToken)
    {
        var lease = _session.Capture();
        if (lease is null || !CanOpenCatch(catchId))
        {
            return;
        }

        _openingCatch = true;
        try
        {
            var result = await _catches.GetAsync(catchId, cancellationToken);
            if (!_session.IsCurrent(lease))
            {
                return;
            }

            if (!result.IsSuccess)
            {
                _statusKey = "DashboardCatchUnavailable";
                Notify();
                return;
            }

            var item = _catchMapper.MapRemote(result.Value!, Text);
            item.SetUnits(_measurement, _measurementUnits.WeightUnit, _measurementUnits.LengthUnit);
            await _navigation.NavigateAsync(MobileRouteEnum.CatchDetail, item: item, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || lease.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Dashboard catch navigation failed with {ErrorType}.", exception.GetType().Name);
            if (_session.IsCurrent(lease))
            {
                _statusKey = "DashboardCatchUnavailable";
                Notify();
            }
        }
        finally
        {
            _openingCatch = false;
        }
    }

    private DateTimeOffset ToUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        var zone = _clock.LocalTimeZone;
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    private DashboardFilterDto BuildFilter(DashboardDateRangeModel range)
    {
        return new DashboardFilterDto
        {
            FromInclusive = range.From is { } start ? ToUtc(start) : null,
            ToExclusive = range.To is { } end ? ToUtc(end.AddDays(1)) : null,
            Species = SelectedSpecies?.Value,
            Method = SelectedMethod?.Value,
            Venue = SelectedVenue?.Value
        };
    }

    private bool IsCurrent(long request, AccountSessionLeaseModel lease)
    {
        return request == _request && _session.IsCurrent(lease);
    }

    private bool CanOpenCatch(Guid catchId)
    {
        return !_openingCatch && _owner is not null && _session.IsCurrent(_owner) && _connectivity.IsOnline
            && LifetimeBests.Concat(FilteredBests).Any(row => row.CatchId == catchId);
    }

    private void RebuildRows()
    {
        LifetimeBests = Data?.AllTimePersonalBests.Select(ToRow).ToArray() ?? [];
        FilteredBests = Data?.FilteredPersonalBests.Select(ToRow).ToArray() ?? [];
        Statistics = Data?.CatchStatistics.Select(row => new DashboardRowModel(
            row.SpeciesName ?? Text["DashboardUnknownSpecies"], row.Method ?? Text["DashboardUnknownMethod"],
            row.Count.ToString(Text.Culture), null)).ToArray() ?? [];
        SpeciesOptions = Choices(Data?.Species ?? []);
        MethodOptions = Choices(Data?.Methods ?? []);
        VenueOptions = Choices(Data?.Venues ?? []);
        SelectedSpecies = SpeciesOptions.FirstOrDefault(choice => choice.Value == SelectedSpecies?.Value);
        SelectedMethod = MethodOptions.FirstOrDefault(choice => choice.Value == SelectedMethod?.Value);
        SelectedVenue = VenueOptions.FirstOrDefault(choice => choice.Value == SelectedVenue?.Value);
    }

    private DashboardRowModel ToRow(DashboardPersonalBestDto row)
    {
        var unit = Text[_measurementUnits.WeightUnit == WeightUnitEnum.Lb ? "MeasureUnitLb" : "MeasureUnitKg"];
        var weight = _measurementUnits.WeightUnit == WeightUnitEnum.Lb
            ? _measurement.FormatWeight(row.Weight, _measurementUnits.WeightUnit, unit, Text["MeasureUnitOz"])
            : string.Format(Text.Culture, "{0:0.##} {1}", _measurement.ToDisplayWeight(row.Weight, _measurementUnits.WeightUnit), unit);
        return new DashboardRowModel(row.SpeciesName ?? Text["DashboardUnknownSpecies"],
            row.Method ?? Text["DashboardUnknownMethod"], weight, row.CatchId);
    }

    private IReadOnlyList<DashboardChoiceModel> Choices(IReadOnlyList<string> values)
    {
        return [new DashboardChoiceModel(null, Text["DashboardAll"]), .. values.Select(value => new DashboardChoiceModel(value, value))];
    }

    private void SetFailure(string key)
    {
        Data = null;
        IsLoading = false;
        _statusKey = key;
        RebuildRows();
        Notify();
    }

    private void ClearAccountData(AccountSessionLeaseModel lease)
    {
        void Clear()
        {
            if (!ReferenceEquals(_owner, lease))
            {
                return;
            }

            Interlocked.Increment(ref _request);
            SelectedSpecies = SelectedMethod = SelectedVenue = null;
            SetFailure("DashboardOffline");
        }

        if (_context is null)
        {
            Clear();
        }
        else
        {
            _context.Post(_ => Clear(), null);
        }
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        RebuildRows();
        Notify();
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private void OnConnectivityChanged(object? sender, EventArgs args)
    {
        if (!_connectivity.IsOnline)
        {
            Interlocked.Increment(ref _request);
            if (_context is null)
            {
                SetFailure("DashboardOffline");
            }
            else
            {
                _context.Post(_ => ClearOfflineData(), null);
            }
        }
    }

    private void ClearOfflineData()
    {
        if (!_connectivity.IsOnline)
        {
            SetFailure("DashboardOffline");
        }
    }

    public void Dispose()
    {
        Text.PropertyChanged -= OnCultureChanged;
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
        _accountCancellation.Dispose();
    }
}
