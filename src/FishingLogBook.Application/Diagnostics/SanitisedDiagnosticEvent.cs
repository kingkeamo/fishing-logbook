using FishingLogBook.Shared.Diagnostics;

namespace FishingLogBook.Application.Diagnostics;

public sealed class SanitisedDiagnosticEvent
{
    public required Guid Id { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    public required DiagnosticLevel Level { get; init; }

    public required string EventName { get; init; }

    public required string Message { get; init; }

    public required Guid CorrelationId { get; init; }

    public Guid AnonymousSessionId { get; init; }

    public string? AppVersion { get; init; }

    public string? Platform { get; init; }

    public bool IsOnline { get; init; }

    public string? Route { get; init; }

    public string? ErrorType { get; init; }

    public string? StackTrace { get; init; }

    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
}
