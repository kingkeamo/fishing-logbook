using System.Globalization;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Browser.Time;

public sealed class TimeService : ITimeService
{
    private const string ModulePath = "./js/time.js";

    private readonly IJSRuntime _jsRuntime;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public TimeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string> ToDateTimeLocalValueAsync(
        DateTimeOffset instant,
        CancellationToken cancellationToken)
    {
        var utcIso = instant.ToUniversalTime().UtcDateTime.ToString(
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            CultureInfo.InvariantCulture);
        var module = await GetModuleAsync(cancellationToken);
        var localValue = await module.InvokeAsync<string?>(
            "toDateTimeLocalValue",
            cancellationToken,
            utcIso);
        return localValue ?? string.Empty;
    }

    public async Task<DateTimeOffset?> FromDateTimeLocalValueAsync(
        string localValue,
        CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        var utcIso = await module.InvokeAsync<string?>(
            "fromDateTimeLocalValue",
            cancellationToken,
            localValue);
        if (string.IsNullOrWhiteSpace(utcIso)
            || !DateTimeOffset.TryParse(
                utcIso,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return null;
        }

        return parsed.ToUniversalTime();
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
}
