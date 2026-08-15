namespace FishingLogBook.Shared.Dtos;

public sealed class ClientDiagnosticEventDto
{
    public Guid Id { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }

    public string Level { get; set; } = string.Empty;

    public string EventName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid CorrelationId { get; set; }

    public Guid AnonymousSessionId { get; set; }

    public string? AppVersion { get; set; }

    public string? Platform { get; set; }

    public bool IsOnline { get; set; }

    public string? Route { get; set; }

    public string? ErrorType { get; set; }

    public string? StackTrace { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }
}
