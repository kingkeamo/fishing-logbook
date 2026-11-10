using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Web.Common.Offline;
using FishingLogBook.Web.Features.Diagnostics.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Offline.OfflineOperationTests;

public class WhenTestingOutcomes
{
    private static Task<T> RunAsync<T>(
        IDiagnosticLogger diagnostics,
        Func<CancellationToken, Task<T>> action,
        ILoggingService? logging = null)
    {
        return OfflineOperation.ExecuteAsync(
            "read",
            "testCatches",
            DiagnosticEventNames.OfflineDbReadStarted,
            DiagnosticEventNames.OfflineDbReadCompleted,
            DiagnosticEventNames.OfflineDbReadFailed,
            DiagnosticEventNames.OfflineDbReadTimedOut,
            TimeSpan.FromSeconds(5),
            diagnostics,
            action,
            CancellationToken.None,
            logging);
    }

    [Fact]
    public async Task ItShouldRecordTheCountOfAnIntegerResult()
    {
        // Arrange
        var diagnostics = Substitute.For<IDiagnosticLogger>();

        // Act
        var result = await RunAsync(diagnostics, _ => Task.FromResult(7));

        // Assert
        result.Should().Be(7);
        await diagnostics.Received(1).LogAsync(
            DiagnosticLevel.Debug,
            DiagnosticEventNames.OfflineDbReadCompleted,
            "read completed.",
            Arg.Is<IReadOnlyDictionary<string, string>?>(metadata =>
                metadata != null && metadata[DiagnosticMetadata.RecordCount] == "7"),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordTheCountOfACollectionResult()
    {
        // Arrange
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        IReadOnlyCollection<string> keys = ["a", "b", "c"];

        // Act
        var result = await RunAsync(diagnostics, _ => Task.FromResult(keys));

        // Assert
        result.Should().BeSameAs(keys);
        await diagnostics.Received(1).LogAsync(
            DiagnosticLevel.Debug,
            DiagnosticEventNames.OfflineDbReadCompleted,
            "read completed.",
            Arg.Is<IReadOnlyDictionary<string, string>?>(metadata =>
                metadata != null && metadata[DiagnosticMetadata.RecordCount] == "3"),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRecordACountForAnyOtherResult()
    {
        // Arrange
        var diagnostics = Substitute.For<IDiagnosticLogger>();

        // Act
        var result = await RunAsync(diagnostics, _ => Task.FromResult("value"));

        // Assert
        result.Should().Be("value");
        await diagnostics.Received(1).LogAsync(
            DiagnosticLevel.Debug,
            DiagnosticEventNames.OfflineDbReadCompleted,
            "read completed.",
            Arg.Is<IReadOnlyDictionary<string, string>?>(metadata =>
                metadata != null && !metadata.ContainsKey(DiagnosticMetadata.RecordCount)),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailureAsAnErrorAndRethrowIt()
    {
        // Arrange
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        var failure = new InvalidOperationException("boom");

        // Act
        var act = () => RunAsync<int>(diagnostics, _ => throw failure);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        await diagnostics.Received(1).LogAsync(
            DiagnosticLevel.Error,
            DiagnosticEventNames.OfflineDbReadFailed,
            "read failed.",
            Arg.Is<IReadOnlyDictionary<string, string>?>(metadata =>
                metadata != null && metadata[DiagnosticMetadata.ErrorType] == nameof(InvalidOperationException)),
            failure,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldReportATimeoutShapedFailureAsATimeoutWarning(bool useTimeoutException)
    {
        // Arrange
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        Exception failure = useTimeoutException
            ? new TimeoutException("slow")
            : new InvalidOperationException("The request TIMED OUT upstream.");

        // Act
        var act = () => RunAsync<int>(diagnostics, _ => throw failure);

        // Assert
        (await act.Should().ThrowAsync<Exception>()).Which.Should().BeSameAs(failure);
        await diagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.OfflineDbReadTimedOut,
            "read timed out.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            failure,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldForwardADiagnosticLoggingFailureToTheErrorLogWithoutFailingTheOperation()
    {
        // Arrange
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        var loggingFailure = new InvalidOperationException("diagnostics down");
        diagnostics.LogAsync(
                Arg.Any<DiagnosticLevel>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(loggingFailure));
        var logging = Substitute.For<ILoggingService>();

        // Act
        var result = await RunAsync(diagnostics, _ => Task.FromResult(1), logging);

        // Assert
        result.Should().Be(1);
        await logging.Received(2).LogErrorAsync(
            "diagnostic log",
            loggingFailure,
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldSwallowADiagnosticLoggingFailureWhenThereIsNoErrorLog()
    {
        // Arrange
        var diagnostics = Substitute.For<IDiagnosticLogger>();
        diagnostics.LogAsync(
                Arg.Any<DiagnosticLevel>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(),
                Arg.Any<Exception?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("diagnostics down")));

        // Act
        var result = await RunAsync(diagnostics, _ => Task.FromResult(2));

        // Assert
        result.Should().Be(2);
        await diagnostics.Received(2).LogAsync(
            Arg.Any<DiagnosticLevel>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }
}
