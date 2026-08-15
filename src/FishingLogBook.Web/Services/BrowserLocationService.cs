using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Diagnostics;
using FishingLogBook.Web.Offline;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Services;

public sealed class BrowserLocationService : ILocationService
{
    private const string ModulePath = "./js/location.js";
    private const int CaptureTimeoutMilliseconds = 8000;

    private readonly IJSRuntime _jsRuntime;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public BrowserLocationService(IJSRuntime jsRuntime, IDiagnosticLogger diagnostics)
    {
        _jsRuntime = jsRuntime;
        _diagnostics = diagnostics;
    }

    public async Task<LocationPromptStatus> GetPromptStatusAsync(CancellationToken cancellationToken)
    {
        var permission = await QueryPermissionAsync(cancellationToken);
        var dismissed = await IsDismissedAsync(cancellationToken);

        return permission switch
        {
            "granted" => new LocationPromptStatus(false, false, true),
            "denied" => new LocationPromptStatus(false, true, false),
            "unavailable" => new LocationPromptStatus(false, true, false),
            _ => dismissed
                ? new LocationPromptStatus(false, true, false)
                : new LocationPromptStatus(true, false, false)
        };
    }

    public async Task DismissPromptAsync(CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("setPromptDismissed", cancellationToken);
    }

    public async Task<TestCatchLocation?> TryCaptureAsync(bool userRequested, CancellationToken cancellationToken)
    {
        var status = await GetPromptStatusAsync(cancellationToken);
        if (!userRequested && !status.WillCaptureOnSave)
        {
            return null;
        }

        try
        {
            var module = await GetModuleAsync(cancellationToken);
            var result = await module.InvokeAsync<LocationJsResult>(
                "getCurrent",
                cancellationToken,
                CaptureTimeoutMilliseconds);

            if (result is null || !string.IsNullOrWhiteSpace(result.Error))
            {
                await LogCaptureOutcomeAsync(result?.Error, cancellationToken);
                if (string.Equals(result?.Error, "denied", StringComparison.OrdinalIgnoreCase))
                {
                    await DismissPromptAsync(cancellationToken);
                }

                return null;
            }

            if (!DateTimeOffset.TryParse(result.Timestamp, out var capturedOn))
            {
                capturedOn = DateTimeOffset.UtcNow;
            }

            return new TestCatchLocation(
                result.Latitude,
                result.Longitude,
                result.Accuracy,
                capturedOn,
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion);
        }
        catch (JSException exception)
        {
            await _diagnostics.LogAsync(
                DiagnosticLevel.Warning,
                DiagnosticEventNames.LocationCaptureFailed,
                "Location capture failed.",
                exception: exception,
                cancellationToken: cancellationToken);
            return null;
        }
    }

    private async Task LogCaptureOutcomeAsync(string? error, CancellationToken cancellationToken)
    {
        if (string.Equals(error, "denied", StringComparison.OrdinalIgnoreCase))
        {
            await _diagnostics.LogAsync(
                DiagnosticLevel.Information,
                DiagnosticEventNames.LocationPermissionDenied,
                "Location permission denied.",
                cancellationToken: cancellationToken);
            return;
        }

        await _diagnostics.LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.LocationCaptureFailed,
            "Location capture failed.",
            cancellationToken: cancellationToken);
    }

    private async Task<string> QueryPermissionAsync(CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        var permission = await module.InvokeAsync<string>("queryPermission", cancellationToken);
        return string.IsNullOrWhiteSpace(permission) ? "prompt" : permission;
    }

    private async Task<bool> IsDismissedAsync(CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<bool>("isPromptDismissed", cancellationToken);
    }

    private async Task<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        await _moduleLock.WaitAsync(cancellationToken);
        try
        {
            return _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                cancellationToken,
                ModulePath);
        }
        finally
        {
            _moduleLock.Release();
        }
    }

    private sealed class LocationJsResult
    {
        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double? Accuracy { get; set; }

        public string? Timestamp { get; set; }

        public string? Error { get; set; }
    }
}
