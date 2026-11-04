using System.Globalization;
using FishingLogBook.Shared.Dashboard.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Dashboard.Clients;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Dashboard.Pages.Dashboard;

public partial class Dashboard : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _requestCancellation;
    private long _request;
    private bool _disposed;
    private DashboardDto? _data;
    private bool _loading = true;
    private string? _statusKey;
    private WeightUnitEnum _weightUnit;
    private DashboardDatePresetEnum _preset = DashboardDatePresetEnum.ThisYear;
    private DateTime _from = new(DateTime.Today.Year, 1, 1);
    private DateTime _to = new(DateTime.Today.Year, 12, 31);
    private string? _species;
    private string? _method;
    private string? _venue;

    [Inject] private IDashboardClient Client { get; set; } = default!;
    [Inject] private INetworkService Network { get; set; } = default!;
    [Inject] private ITimeService Time { get; set; } = default!;
    [Inject] private IAnglerPreferencesProvider Preferences { get; set; } = default!;
    [Inject] private AuthenticationStateProvider Authentication { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string LifetimeEmpty => Loc[_data?.Lifetime.Catches == 0 ? "DashboardFirstBest" : "DashboardWeightNeeded"];
    private string StatisticsEmpty => Loc[_data?.Lifetime.Catches == 0 ? "DashboardNoCatches" : "DashboardNoMatches"];
    private string FilteredEmpty => _data?.CatchStatistics.Count == 0 ? StatisticsEmpty : Loc["DashboardWeightNeeded"];

    protected override async Task OnInitializedAsync()
    {
        Authentication.AuthenticationStateChanged += OnAuthenticationChanged;
        Network.ConnectivityChanged += OnConnectivityChanged;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var request = ++_request;
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _requestCancellation.Token;
        _loading = true;
        _statusKey = null;
        try
        {
            if (!await Network.IsOnlineAsync(token))
            {
                Fail("DashboardOffline");
                return;
            }

            var filter = await BuildFilterAsync(token);
            if (filter is null)
            {
                _statusKey = "DashboardInvalidDates";
                return;
            }

            var data = await Client.GetAsync(filter, token);
            var preferences = await Preferences.GetAsync(token);
            if (request != _request || token.IsCancellationRequested)
            {
                return;
            }

            _data = data;
            _weightUnit = preferences.WeightUnit;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (request == _request)
            {
                Fail("DashboardError");
                await Logging.LogErrorAsync("dashboard load", exception.GetType().Name, CancellationToken.None);
            }
        }
        finally
        {
            if (request == _request)
            {
                _loading = false;
            }
        }
    }

    private async Task<DashboardFilterDto?> BuildFilterAsync(CancellationToken token)
    {
        var localNow = await Time.ToDateTimeLocalValueAsync(DateTimeOffset.UtcNow, token);
        var today = DateOnly.ParseExact(localNow[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var range = DashboardDateRangeModel.Resolve(_preset, today, DateOnly.FromDateTime(_from), DateOnly.FromDateTime(_to));
        if (range.From > range.To || range.To == DateOnly.MaxValue)
        {
            return null;
        }

        var from = await BoundaryAsync(range.From, token);
        var to = await BoundaryAsync(range.To?.AddDays(1), token);
        if ((range.From.HasValue && !from.HasValue) || (range.To.HasValue && !to.HasValue))
        {
            return null;
        }

        return new DashboardFilterDto
        {
            FromInclusive = from,
            ToExclusive = to,
            Species = NullIfEmpty(_species),
            Method = NullIfEmpty(_method),
            Venue = NullIfEmpty(_venue)
        };
    }

    private async Task<DateTimeOffset?> BoundaryAsync(DateOnly? date, CancellationToken token)
    {
        return date is null ? null : await Time.FromDateTimeLocalValueAsync(
            date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00", token);
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private async Task ResetAsync()
    {
        _preset = DashboardDatePresetEnum.ThisYear;
        _species = _method = _venue = null;
        await LoadAsync();
    }

    private void Fail(string key)
    {
        _data = null;
        _statusKey = key;
    }

    private void OnAuthenticationChanged(Task<AuthenticationState> state)
    {
        Invalidate("DashboardError");
    }

    private void OnConnectivityChanged(bool online)
    {
        if (!online)
        {
            Invalidate("DashboardOffline");
        }
    }

    private void Invalidate(string key)
    {
        ++_request;
        _requestCancellation?.Cancel();
        _data = null;
        _loading = false;
        _statusKey = key;
        if (!_disposed)
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Authentication.AuthenticationStateChanged -= OnAuthenticationChanged;
        Network.ConnectivityChanged -= OnConnectivityChanged;
        _lifetime.Cancel();
        _requestCancellation?.Cancel();
        _requestCancellation?.Dispose();
        _lifetime.Dispose();
    }
}
