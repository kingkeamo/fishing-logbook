using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;

namespace FishingLogBook.Web.Tests.JavaScript.OfflineStore;

public class WhenTestingDiagnosticEvents : BaseOfflineStoreTest
{
    [Fact]
    public void ItShouldEmitIndexedDbLifecycleEventsWithoutPrivateData()
    {
        // Arrange
        var script = ReadOfflineStoreScript();

        // Act
        var diagnosticScript = script[..script.IndexOf(
            "export async function putCatchWithPhotographs",
            StringComparison.Ordinal)];

        // Assert
        diagnosticScript.Should().Contain(DiagnosticEventNames.OfflineDbOpenStarted);
        diagnosticScript.Should().Contain(DiagnosticEventNames.OfflineDbOpenCompleted);
        diagnosticScript.Should().Contain(DiagnosticEventNames.OfflineDbOpenFailed);
        diagnosticScript.Should().Contain(DiagnosticEventNames.OfflineDbTransactionStarted);
        diagnosticScript.Should().Contain(DiagnosticEventNames.OfflineDbRequestSucceeded);
        diagnosticScript.Should().Contain(DiagnosticEventNames.OfflineDbTransactionCompleted);
        diagnosticScript.Should().Contain(DiagnosticEventNames.OfflineDbTransactionAborted);
        diagnosticScript.Should().Contain(DiagnosticEventNames.OfflineDbTransactionError);
        diagnosticScript.Should().Contain(DiagnosticEventNames.OfflineDbClosed);
        diagnosticScript.Should().Contain("navigator.storage.estimate");
        diagnosticScript.Should().Contain("elapsedMilliseconds");
        diagnosticScript.Should().NotContain("species");
        diagnosticScript.Should().NotContain("latitude");
        diagnosticScript.Should().NotContain("base64,");
    }
}
