using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Storage;

namespace FishingLogBook.Web.Features.Diagnostics.Services;

public interface ILoggingService
{
    Task LogErrorAsync(string source, Exception exception, CancellationToken cancellationToken = default);

    Task LogErrorAsync(string source, string message, CancellationToken cancellationToken = default);

    Task<LastErrorLog?> GetLastErrorAsync(CancellationToken cancellationToken = default);
}

public sealed class LastErrorLog
{
    public DateTimeOffset TimestampUtc { get; set; }

    public string Source { get; set; } = string.Empty;

    public string? ErrorType { get; set; }

    public string Message { get; set; } = string.Empty;
}
