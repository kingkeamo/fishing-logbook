using FishingLogBook.Application.Diagnostics.Contracts.Services;
using FishingLogBook.Domain.Config;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FishingLogBook.Application.Diagnostics;

public sealed class DiagnosticLogService
{
    private readonly DiagnosticsConfig _config;
    private readonly IDiagnosticEventDeduplicator _deduplicator;
    private readonly ILogger<DiagnosticLogService> _logger;

    public DiagnosticLogService(
        IOptions<DiagnosticsConfig> config,
        IDiagnosticEventDeduplicator deduplicator,
        ILogger<DiagnosticLogService> logger)
    {
        _config = config.Value;
        _deduplicator = deduplicator;
        _logger = logger;
    }

    public Task<DiagnosticAcceptResult> AcceptAsync(
        ClientDiagnosticBatchDto? batch,
        Guid requestCorrelationId,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (batch?.Events is null)
        {
            return Task.FromResult(DiagnosticAcceptResult.Invalid("Diagnostic batch is required."));
        }

        if (batch.Events.Count == 0 || batch.Events.Count > _config.MaxBatchSize)
        {
            return Task.FromResult(DiagnosticAcceptResult.Invalid("Diagnostic batch size is outside the allowed range."));
        }

        var acceptedCount = 0;
        foreach (var incoming in batch.Events)
        {
            var sanitised = Sanitise(incoming, requestCorrelationId);
            if (sanitised is null)
            {
                return Task.FromResult(DiagnosticAcceptResult.Invalid("Diagnostic event is malformed."));
            }

            if (!_deduplicator.TryAccept(sanitised.Id))
            {
                continue;
            }

            acceptedCount++;
            WriteStructuredLog(sanitised, requestCorrelationId);
        }

        return Task.FromResult(DiagnosticAcceptResult.Accepted(acceptedCount));
    }

    private SanitisedDiagnosticEvent? Sanitise(ClientDiagnosticEventDto incoming, Guid requestCorrelationId)
    {
        if (incoming.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(incoming.EventName) ||
            incoming.EventName.Length > _config.MaxEventNameLength)
        {
            return null;
        }

        if (!TryParseLevel(incoming.Level, out var level))
        {
            return null;
        }

        var correlationId = incoming.CorrelationId == Guid.Empty ? requestCorrelationId : incoming.CorrelationId;
        var timestamp = incoming.TimestampUtc == default ? DateTimeOffset.UtcNow : incoming.TimestampUtc.ToUniversalTime();

        return new SanitisedDiagnosticEvent
        {
            Id = incoming.Id,
            TimestampUtc = timestamp,
            Level = level,
            EventName = incoming.EventName.Trim(),
            Message = DiagnosticMetadata.Truncate(incoming.Message ?? string.Empty, _config.MaxMessageLength),
            CorrelationId = correlationId,
            AnonymousSessionId = incoming.AnonymousSessionId,
            AppVersion = DiagnosticMetadata.Truncate(incoming.AppVersion ?? string.Empty, 40),
            Platform = DiagnosticMetadata.Truncate(incoming.Platform ?? string.Empty, 120),
            IsOnline = incoming.IsOnline,
            Route = DiagnosticMetadata.Truncate(incoming.Route ?? string.Empty, 80),
            ErrorType = DiagnosticMetadata.Truncate(incoming.ErrorType ?? string.Empty, 80),
            StackTrace = string.IsNullOrWhiteSpace(incoming.StackTrace)
                ? null
                : DiagnosticMetadata.Truncate(incoming.StackTrace, _config.MaxStackTraceLength),
            Metadata = DiagnosticMetadata.Filter(incoming.Metadata)
        };
    }

    private void WriteStructuredLog(SanitisedDiagnosticEvent diagnostic, Guid requestCorrelationId)
    {
        var scope = new Dictionary<string, object?>
        {
            ["CorrelationId"] = diagnostic.CorrelationId,
            ["RequestCorrelationId"] = requestCorrelationId,
            ["DiagnosticEventId"] = diagnostic.Id,
            ["EventName"] = diagnostic.EventName,
            ["AnonymousSessionId"] = diagnostic.AnonymousSessionId,
            ["AppVersion"] = diagnostic.AppVersion,
            ["Platform"] = diagnostic.Platform,
            ["IsOnline"] = diagnostic.IsOnline,
            ["Route"] = diagnostic.Route,
            ["ErrorType"] = diagnostic.ErrorType
        };

        foreach (var pair in diagnostic.Metadata)
        {
            scope[$"diag.{pair.Key}"] = pair.Value;
        }

        using (_logger.BeginScope(scope))
        {
            _logger.Log(
                ToLogLevel(diagnostic.Level),
                "Client diagnostic {EventName}: {Message}",
                diagnostic.EventName,
                diagnostic.Message);
        }
    }

    private static bool TryParseLevel(string? value, out DiagnosticLevel level)
    {
        return Enum.TryParse(value, ignoreCase: true, out level) && Enum.IsDefined(level);
    }

    private static LogLevel ToLogLevel(DiagnosticLevel level)
    {
        return level switch
        {
            DiagnosticLevel.Debug => LogLevel.Debug,
            DiagnosticLevel.Information => LogLevel.Information,
            DiagnosticLevel.Warning => LogLevel.Warning,
            DiagnosticLevel.Error => LogLevel.Error,
            DiagnosticLevel.Critical => LogLevel.Critical,
            _ => LogLevel.Warning
        };
    }
}

public sealed record DiagnosticAcceptResult(bool IsValid, string? Error, int AcceptedCount)
{
    public static DiagnosticAcceptResult Invalid(string error) => new(false, error, 0);

    public static DiagnosticAcceptResult Accepted(int count) => new(true, null, count);
}
