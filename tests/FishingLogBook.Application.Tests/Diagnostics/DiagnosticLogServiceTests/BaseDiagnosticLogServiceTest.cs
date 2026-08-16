using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Diagnostics;
using FishingLogBook.Domain.Config;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Diagnostics.DiagnosticLogServiceTests;

public class BaseDiagnosticLogServiceTest
{
    protected readonly IDiagnosticEventDeduplicator MockDeduplicator =
        Substitute.For<IDiagnosticEventDeduplicator>();

    protected DiagnosticLogService CreateSut(RecordingLogger<DiagnosticLogService> logger)
    {
        MockDeduplicator.TryAccept(Arg.Any<Guid>()).Returns(true);
        return new DiagnosticLogService(
            Options.Create(new DiagnosticsConfig { MaxBatchSize = 50 }),
            MockDeduplicator,
            logger);
    }
}
