using System.Text.Json;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Diagnostics.Services;

public sealed class LoggingService : ILoggingService
{
    private const string SetLastError = "fishingLogBookDiagnostics.setLastError";
    private const string GetLastError = "fishingLogBookDiagnostics.getLastError";
    private const string WriteDiagnostic = "fishingLogBookDiagnostics.console";
    private const int MaxMessageLength = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IJSRuntime _jsRuntime;

    public LoggingService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public Task LogErrorAsync(string source, Exception exception, CancellationToken cancellationToken = default)
    {
        return LogErrorAsync(source, exception.GetType().Name, exception.Message, cancellationToken);
    }

    public Task LogErrorAsync(string source, string message, CancellationToken cancellationToken = default)
    {
        return LogErrorAsync(source, null, message, cancellationToken);
    }

    public async Task<LastErrorLog?> GetLastErrorAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>(GetLastError, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<LastErrorLog>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task LogErrorAsync(
        string source,
        string? errorType,
        string message,
        CancellationToken cancellationToken)
    {
        var lastError = new LastErrorLog
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Source = DiagnosticMetadata.Truncate(source, 80),
            ErrorType = errorType,
            Message = DiagnosticMetadata.Truncate(message, MaxMessageLength)
        };

        try
        {
            await _jsRuntime.InvokeVoidAsync(
                WriteDiagnostic,
                cancellationToken,
                "Error",
                lastError.Source,
                $"{lastError.ErrorType}: {lastError.Message}");
        }
        catch
        {
            // Diagnostics must never replace the original application failure.
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync(
                SetLastError,
                cancellationToken,
                JsonSerializer.Serialize(lastError, JsonOptions));
        }
        catch
        {
            // Persisting diagnostics is best effort and must not recurse into logging.
        }
    }
}
