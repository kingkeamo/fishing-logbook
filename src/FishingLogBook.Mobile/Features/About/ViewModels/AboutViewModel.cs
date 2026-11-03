using System.ComponentModel;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Configuration;
using FishingLogBook.Mobile.Features.About.Clients;
using FishingLogBook.Mobile.Features.About.Enums;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.About.ViewModels;

public sealed class AboutViewModel : INotifyPropertyChanged
{
    private const string HealthyStatus = "Healthy";
    private const string DegradedStatus = "Degraded";

    private readonly ISystemStatusClient _status;
    private readonly IAppInfoAdapter _app;
    private readonly IClipboardAdapter _clipboard;
    private readonly IConnectivityService _connectivity;
    private readonly MobileAuthConfig _auth;
    private StatusStateEnum _apiStatus = StatusStateEnum.Checking;
    private StatusStateEnum _databaseStatus = StatusStateEnum.Checking;
    private string? _databaseName;
    private string? _apiVersion;
    private string? _apiCommit;
    private string? _apiEnvironment;
    private bool _isChecking;
    private bool _copied;

    public AboutViewModel(
        ISystemStatusClient status,
        IAppInfoAdapter app,
        IClipboardAdapter clipboard,
        IConnectivityService connectivity,
        MobileAuthConfig auth,
        UiTextService text)
    {
        _status = status;
        _app = app;
        _clipboard = clipboard;
        _connectivity = connectivity;
        _auth = auth;
        Text = text;
        text.PropertyChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public string AppVersion => _app.Version;

    public string AppBuild => _app.Build;

    public string Platform => string.IsNullOrWhiteSpace(_app.PlatformVersion)
        ? _app.Platform
        : $"{_app.Platform} {_app.PlatformVersion}";

    public string EnvironmentName => EnvironmentFrom(_auth.ApiBaseUrl);

    public string AppCommit => string.IsNullOrWhiteSpace(_app.Commit) ? Text["AboutUnavailable"] : _app.Commit;

    public bool IsChecking => _isChecking;

    public bool CanRefresh => !_isChecking;

    public string ApiStatusText => StatusText(_apiStatus);

    public string DatabaseStatusText => DatabaseText();

    public string ApiVersion => _apiVersion ?? Text["AboutUnavailable"];

    public string ApiCommit => _apiCommit ?? Text["AboutUnavailable"];

    public string ApiEnvironment => _apiEnvironment ?? Text["AboutUnavailable"];

    public bool Copied => _copied;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _isChecking = true;
        _copied = false;
        _apiStatus = StatusStateEnum.Checking;
        _databaseStatus = StatusStateEnum.Checking;
        _databaseName = null;
        _apiVersion = null;
        _apiCommit = null;
        _apiEnvironment = null;
        Notify();
        if (!_connectivity.IsOnline)
        {
            _apiStatus = StatusStateEnum.DeviceOffline;
            _databaseStatus = StatusStateEnum.DeviceOffline;
            _isChecking = false;
            Notify();
            return;
        }

        _apiStatus = await ReadApiAsync(cancellationToken);
        if (_apiStatus == StatusStateEnum.Online)
        {
            await ReadApiBuildAsync(cancellationToken);
            _databaseStatus = await ReadDatabaseAsync(cancellationToken);
        }
        else
        {
            _databaseStatus = _apiStatus;
        }

        _isChecking = false;
        Notify();
    }

    public async Task CopyDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var lines = new[]
        {
            $"{Text["AboutPlatform"]}: {Platform}",
            $"{Text["AboutVersion"]}: {AppVersion}",
            $"{Text["AboutBuildNumber"]}: {AppBuild}",
            $"{Text["AboutCommit"]}: {AppCommit}",
            $"{Text["AboutEnvironment"]}: {EnvironmentName}",
            $"{Text["AboutApi"]}: {ApiStatusText}",
            $"{Text["AboutApiVersion"]}: {ApiVersion}",
            $"{Text["AboutApiEnvironment"]}: {ApiEnvironment}",
            $"{Text["AboutDatabase"]}: {DatabaseStatusText}"
        };
        await _clipboard.SetTextAsync(string.Join(Environment.NewLine, lines));
        _copied = true;
        Notify();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<StatusStateEnum> ReadApiAsync(CancellationToken cancellationToken)
    {
        var result = await _status.GetApiHealthAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Outcome is ApiOutcomeEnum.Unavailable or ApiOutcomeEnum.SessionExpired
                ? StatusStateEnum.Unreachable
                : StatusStateEnum.Offline;
        }

        return string.Equals(result.Value?.Status, HealthyStatus, StringComparison.Ordinal)
            ? StatusStateEnum.Online
            : StatusStateEnum.Offline;
    }

    private async Task ReadApiBuildAsync(CancellationToken cancellationToken)
    {
        var result = await _status.GetBuildMetadataAsync(cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return;
        }

        _apiVersion = result.Value.Version;
        _apiCommit = result.Value.Sha;
        _apiEnvironment = result.Value.Environment;
    }

    private async Task<StatusStateEnum> ReadDatabaseAsync(CancellationToken cancellationToken)
    {
        var result = await _status.GetDatabaseStatusAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Outcome is ApiOutcomeEnum.ServerError
                ? StatusStateEnum.Offline
                : StatusStateEnum.Unreachable;
        }

        _databaseName = result.Value?.Name;
        return result.Value?.Status switch
        {
            HealthyStatus => StatusStateEnum.Online,
            DegradedStatus => StatusStateEnum.Degraded,
            _ => StatusStateEnum.Offline
        };
    }

    private string DatabaseText()
    {
        var status = StatusText(_databaseStatus);
        return string.IsNullOrWhiteSpace(_databaseName) ? status : $"{status} · {_databaseName}";
    }

    private string StatusText(StatusStateEnum status)
    {
        return status switch
        {
            StatusStateEnum.Checking => Text["AboutChecking"],
            StatusStateEnum.Online => Text["AboutOnline"],
            StatusStateEnum.Degraded => Text["AboutDegraded"],
            StatusStateEnum.Offline => Text["AboutOffline"],
            StatusStateEnum.Unreachable => Text["AboutUnreachable"],
            StatusStateEnum.DeviceOffline => Text["AboutDeviceOffline"],
            _ => Text["AboutUnavailable"]
        };
    }

    private string EnvironmentFrom(string apiBaseUrl)
    {
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri))
        {
            return apiBaseUrl;
        }

        if (uri.Host is "localhost" or "127.0.0.1" or "10.0.2.2")
        {
            return Text["AboutEnvironmentLocal"];
        }

        if (uri.Host.Contains("-dev-", StringComparison.OrdinalIgnoreCase))
        {
            return Text["AboutEnvironmentDevelopment"];
        }

        return uri.Host;
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
