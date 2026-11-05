using System.Globalization;
using FishingLogBook.Shared.Dashboard.Models;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Dashboard.Clients;
using FishingLogBook.Web.Features.Dashboard.Models;
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
    private long _authentication;
    private bool _disposed;
    private DashboardDto? _data;
    private bool _loading = true;
    private string? _statusKey;
    private WeightUnitEnum _weightUnit;
    private DashboardSelectionModel _applied = DashboardSelectionModel.CreateDefault();
    private DashboardSelectionModel? _submitted;
    private DashboardDatePresetEnum _preset;
    private DateTime _from;
    private DateTime _to;
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
    private DashboardSelectionModel Draft => new(_preset, _from, _to, _species, _method, _venue);

    protected override async Task OnInitializedAsync()
    {
        ApplyDraft(_applied);
        Authentication.AuthenticationStateChanged += OnAuthenticationChanged;
        Network.ConnectivityChanged += OnConnectivityChanged;
        await LoadAsync(_applied);
    }

    private void ApplyDraft(DashboardSelectionModel selection)
    {
        _preset = selection.Preset;
        _from = selection.From;
        _to = selection.To;
        _species = selection.Species;
        _method = selection.Method;
        _venue = selection.Venue;
    }

    private async Task LoadAsync(DashboardSelectionModel selection)
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
                if (request == _request)
                {
                    Fail("DashboardOffline");
                }

                return;
            }

            var filter = await BuildFilterAsync(selection, token);
            if (request != _request || token.IsCancellationRequested)
            {
                return;
            }

            if (filter is null)
            {
                Fail("DashboardInvalidDates");
                return;
            }

            _submitted = selection;
            var data = await Client.GetAsync(filter, token);
            var preferences = await Preferences.GetAsync(token);
            if (request != _request || token.IsCancellationRequested)
            {
                return;
            }

            _data = data;
            _weightUnit = preferences.WeightUnit;
            _applied = selection;
            _submitted = null;
            ApplyDraft(selection);
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
                if (!_disposed)
                {
                    StateHasChanged();
                }
            }
        }
    }

    private async Task<DashboardFilterDto?> BuildFilterAsync(DashboardSelectionModel selection, CancellationToken token)
    {
        var localNow = await Time.ToDateTimeLocalValueAsync(DateTimeOffset.UtcNow, token);
        var today = DateOnly.ParseExact(localNow[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var range = DashboardDateRangeModel.Resolve(
            selection.Preset,
            today,
            DateOnly.FromDateTime(selection.From),
            DateOnly.FromDateTime(selection.To));
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
            Species = NullIfEmpty(selection.Species),
            Method = NullIfEmpty(selection.Method),
            Venue = NullIfEmpty(selection.Venue)
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

    private async Task ApplyAsync()
    {
        if (!_loading)
        {
            await LoadAsync(Draft);
        }
    }

    private async Task ResetAsync()
    {
        if (!_loading)
        {
            await LoadAsync(DashboardSelectionModel.CreateDefault());
        }
    }

    private async Task RetryAsync()
    {
        await LoadAsync(_submitted ?? _applied);
    }

    private void Fail(string key)
    {
        _statusKey = key;
    }

    private void OnAuthenticationChanged(Task<AuthenticationState> state)
    {
        if (!_disposed)
        {
            _ = InvokeAsync(() => HandleAuthenticationAsync(state));
        }
    }

    private async Task HandleAuthenticationAsync(Task<AuthenticationState> stateTask)
    {
        var version = ++_authentication;
        Invalidate(null);
        _applied = DashboardSelectionModel.CreateDefault();
        _submitted = null;
        ApplyDraft(_applied);
        StateHasChanged();
        var state = await stateTask;
        if (_disposed || version != _authentication)
        {
            return;
        }

        if (state.User.Identity?.IsAuthenticated == true)
        {
            await LoadAsync(_applied);
            return;
        }

        _statusKey = "DashboardError";
        StateHasChanged();
    }

    private void OnConnectivityChanged(bool online)
    {
        if (_disposed)
        {
            return;
        }

        if (online)
        {
            _ = InvokeAsync(ReloadWhenOnlineAsync);
            return;
        }

        _ = InvokeAsync(GoOffline);
    }

    private void GoOffline()
    {
        Invalidate("DashboardOffline");
        StateHasChanged();
    }

    private async Task ReloadWhenOnlineAsync()
    {
        if (_disposed || _loading || (_data is not null && _statusKey is null))
        {
            return;
        }

        var version = _authentication;
        var state = await Authentication.GetAuthenticationStateAsync();
        if (_disposed || _loading || version != _authentication || state.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        await LoadAsync(_submitted ?? _applied);
    }

    private void Invalidate(string? key)
    {
        ++_request;
        _requestCancellation?.Cancel();
        _data = null;
        _loading = false;
        _statusKey = key;
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
