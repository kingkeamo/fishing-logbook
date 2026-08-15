using FishingLogBook.Shared.Diagnostics;

namespace FishingLogBook.Web.Diagnostics;

public interface IDiagnosticLogger
{
    Task LogAsync(
        DiagnosticLevel level,
        string eventName,
        string message,
        IReadOnlyDictionary<string, string>? metadata = null,
        Exception? exception = null,
        CancellationToken cancellationToken = default);
}
