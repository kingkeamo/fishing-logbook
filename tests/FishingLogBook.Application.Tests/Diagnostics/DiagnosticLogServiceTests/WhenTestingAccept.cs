using AwesomeAssertions;
using FishingLogBook.Application.Diagnostics;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Diagnostics.DiagnosticLogServiceTests;

public class WhenTestingAccept : BaseDiagnosticLogServiceTest
{
    [Fact]
    public async Task ItShouldRejectOversizedBatches()
    {
        // Arrange
        var sut = CreateSut(new RecordingLogger<DiagnosticLogService>());
        var batch = new ClientDiagnosticBatchDto
        {
            Events = Enumerable.Range(0, 51).Select(_ => ValidEvent()).ToArray()
        };

        // Act
        var result = await sut.AcceptAsync(batch, Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        MockDeduplicator.DidNotReceive().TryAccept(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ItShouldStripSensitiveMetadata()
    {
        // Arrange
        var logger = new RecordingLogger<DiagnosticLogService>();
        var sut = CreateSut(logger);
        var diagnostic = ValidEvent(metadata: new Dictionary<string, string>
        {
            ["notes"] = "secret catch notes",
            ["latitude"] = "53.1",
            ["elapsedMilliseconds"] = "12",
            ["photograph"] = "base64"
        });
        var batch = new ClientDiagnosticBatchDto
        {
            Events = [diagnostic]
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
        MockDeduplicator.Received(1).TryAccept(diagnostic.Id);
    }

    [Fact]
    public async Task ItShouldIgnoreDuplicateEventIds()
    {
        // Arrange
        var logger = new RecordingLogger<DiagnosticLogService>();
        var sut = CreateSut(logger);
        var diagnostic = ValidEvent();
        MockDeduplicator.TryAccept(diagnostic.Id).Returns(true, false);
        var batch = new ClientDiagnosticBatchDto { Events = [diagnostic] };

        // Act
        await sut.AcceptAsync(batch, Guid.NewGuid(), CancellationToken.None);
        await sut.AcceptAsync(batch, Guid.NewGuid(), CancellationToken.None);

        // Assert
        logger.Messages.Should().ContainSingle();
        MockDeduplicator.Received(2).TryAccept(diagnostic.Id);
    }

    [Fact]
    public async Task ItShouldUseTheIncomingCorrelationId()
    {
        // Arrange
        var logger = new RecordingLogger<DiagnosticLogService>();
        var sut = CreateSut(logger);
        var correlationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var diagnostic = ValidEvent(correlationId: correlationId);
        var batch = new ClientDiagnosticBatchDto
        {
            Events = [diagnostic]
        };

        // Act
        await sut.AcceptAsync(batch, Guid.NewGuid(), CancellationToken.None);

        // Assert
        logger.Scopes.Should().Contain(scope =>
            scope.Any(pair => pair.Key == "CorrelationId" && Equals(pair.Value, correlationId)));
        MockDeduplicator.Received(1).TryAccept(diagnostic.Id);
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
