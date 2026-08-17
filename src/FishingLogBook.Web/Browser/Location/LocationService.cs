using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Browser.Location;

public sealed class LocationService : ILocationService
{
    public const int CaptureTimeoutMilliseconds = 8000;
    public const int PermissionQueryTimeoutMilliseconds = 2000;

    private const string ModulePath = "./js/location.js";

    private static readonly LocationPromptStatus UnavailablePrompt = new(false, true, false);

    private readonly IJSRuntime _jsRuntime;
    private readonly IDiagnosticLogger _diagnostics;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public LocationService(IJSRuntime jsRuntime, IDiagnosticLogger diagnostics)
    {
        _jsRuntime = jsRuntime;
        _diagnostics = diagnostics;
    }

    public async Task<LocationPromptStatus> GetPromptStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(PermissionQueryTimeoutMilliseconds);
            var permission = await QueryPermissionAsync(timeoutSource.Token);
            var dismissed = await IsDismissedAsync(timeoutSource.Token);
            return ToPromptStatus(permission, dismissed);
        }
        catch (Exception exception) when (IsBoundedFailure(exception, cancellationToken))
        {
            return UnavailablePrompt;
        }
    }

    public async Task DismissPromptAsync(CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("setPromptDismissed", cancellationToken);
    }

    public async Task<CatchLocationModel?> TryCaptureAsync(bool userRequested, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(CaptureTimeoutMilliseconds);
            return await CaptureAsync(userRequested, timeoutSource.Token);
        }
        catch (Exception exception) when (IsBoundedFailure(exception, cancellationToken))
        {
            await LogCaptureOutcomeAsync("timeout", cancellationToken);
            return null;
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

    private async Task<CatchLocationModel?> CaptureAsync(bool userRequested, CancellationToken cancellationToken)
    {
        var status = await GetPromptStatusAsync(cancellationToken);
        if (!userRequested && !status.WillCaptureOnSave)
        {
            return null;
        }

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

        if (!CatchLocationConstants.AreCoordinatesValid(result.Latitude, result.Longitude))
        {
            await LogCaptureOutcomeAsync("unavailable", cancellationToken);
            return null;
        }

        return new CatchLocationModel(
            result.Latitude,
            result.Longitude,
            result.Accuracy,
            capturedOn,
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
    }

    private static LocationPromptStatus ToPromptStatus(string permission, bool dismissed)
    {
        return permission switch
        {
            "granted" => new LocationPromptStatus(false, false, true),
            "denied" => UnavailablePrompt,
            "unavailable" => UnavailablePrompt,
            _ => dismissed
                ? UnavailablePrompt
                : new LocationPromptStatus(true, false, false)
        };
    }

    private static bool IsBoundedFailure(Exception exception, CancellationToken cancellationToken)
    {
        return exception is TimeoutException ||
               (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested) ||
               exception.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
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
