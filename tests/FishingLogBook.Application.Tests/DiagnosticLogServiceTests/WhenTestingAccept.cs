using AwesomeAssertions;
using FishingLogBook.Application.Diagnostics;
using FishingLogBook.Domain.Config;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Options;

namespace FishingLogBook.Application.Tests.DiagnosticLogServiceTests;

public class WhenTestingAccept
{
    [Fact]
    public async Task ItShouldRejectOversizedBatches()
    {
        // Arrange
        var sut = CreateSut(out _);
        var batch = new ClientDiagnosticBatchDto
        {
            Events = Enumerable.Range(0, 51).Select(_ => ValidEvent()).ToArray()
        };

        // Act
        var result = await sut.AcceptAsync(batch, Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldStripSensitiveMetadata()
    {
        // Arrange
        var logger = new RecordingLogger<DiagnosticLogService>();
        var sut = CreateSut(logger);
        var batch = new ClientDiagnosticBatchDto
        {
            Events =
            [
                ValidEvent(metadata: new Dictionary<string, string>
                {
                    ["notes"] = "secret catch notes",
                    ["latitude"] = "53.1",
                    ["elapsedMilliseconds"] = "12",
                    ["photograph"] = "base64"
                })
            ]
        };

        // Act
        var result = await sut.AcceptAsync(batch, Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        logger.Scopes.Should().Contain(scope =>
            scope.Any(pair => pair.Key == "diag.elapsedMilliseconds" && Equals(pair.Value, "12")));
        logger.Scopes.Should().NotContain(scope =>
            scope.Any(pair => pair.Key.Contains("notes", StringComparison.OrdinalIgnoreCase)));
        logger.Scopes.Should().NotContain(scope =>
            scope.Any(pair => pair.Key.Contains("latitude", StringComparison.OrdinalIgnoreCase)));
        logger.Scopes.Should().NotContain(scope =>
            scope.Any(pair => pair.Key.Contains("photograph", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ItShouldIgnoreDuplicateEventIds()
    {
        // Arrange
        var sut = CreateSut(out var logger);
        var diagnostic = ValidEvent();
        var batch = new ClientDiagnosticBatchDto { Events = [diagnostic] };

        // Act
        await sut.AcceptAsync(batch, Guid.NewGuid(), CancellationToken.None);
        await sut.AcceptAsync(batch, Guid.NewGuid(), CancellationToken.None);

        // Assert
        logger.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldUseTheIncomingCorrelationId()
    {
        // Arrange
        var logger = new RecordingLogger<DiagnosticLogService>();
        var sut = CreateSut(logger);
        var correlationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var batch = new ClientDiagnosticBatchDto
        {
            Events = [ValidEvent(correlationId: correlationId)]
        };

        // Act
        await sut.AcceptAsync(batch, Guid.NewGuid(), CancellationToken.None);

        // Assert
        logger.Scopes.Should().Contain(scope =>
            scope.Any(pair => pair.Key == "CorrelationId" && Equals(pair.Value, correlationId)));
    }

    private static DiagnosticLogService CreateSut(out RecordingLogger<DiagnosticLogService> logger)
    {
        logger = new RecordingLogger<DiagnosticLogService>();
        return CreateSut(logger);
    }

    private static DiagnosticLogService CreateSut(RecordingLogger<DiagnosticLogService> logger)
    {
        return new DiagnosticLogService(
            Options.Create(new DiagnosticsConfig { MaxBatchSize = 50 }),
            new MemoryDeduplicator(),
            logger);
    }

    private static ClientDiagnosticEventDto ValidEvent(
        Guid? correlationId = null,
        Dictionary<string, string>? metadata = null)
    {
        return new ClientDiagnosticEventDto
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTimeOffset.UtcNow,
            Level = "Warning",
            EventName = "OfflineDbWriteTimedOut",
            Message = "write timed out",
            CorrelationId = correlationId ?? Guid.NewGuid(),
            AnonymousSessionId = Guid.NewGuid(),
            Metadata = metadata
        };
    }
}
