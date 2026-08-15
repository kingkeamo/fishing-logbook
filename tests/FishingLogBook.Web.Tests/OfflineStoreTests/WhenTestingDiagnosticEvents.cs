using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;

namespace FishingLogBook.Web.Tests.OfflineStoreTests;

public class WhenTestingDiagnosticEvents : BaseOfflineStoreTest
{
    [Fact]
    public void ItShouldEmitIndexedDbLifecycleEventsWithoutPrivateData()
    {
        // Arrange
        var script = ReadOfflineStoreScript();

        // Act
        // Assert
        script.Should().Contain(DiagnosticEventNames.OfflineDbOpenStarted);
        script.Should().Contain(DiagnosticEventNames.OfflineDbOpenCompleted);
        script.Should().Contain(DiagnosticEventNames.OfflineDbOpenFailed);
        script.Should().Contain(DiagnosticEventNames.OfflineDbTransactionStarted);
        script.Should().Contain(DiagnosticEventNames.OfflineDbRequestSucceeded);
        script.Should().Contain(DiagnosticEventNames.OfflineDbTransactionCompleted);
        script.Should().Contain(DiagnosticEventNames.OfflineDbTransactionAborted);
        script.Should().Contain(DiagnosticEventNames.OfflineDbTransactionError);
        script.Should().Contain(DiagnosticEventNames.OfflineDbClosed);
        script.Should().Contain("navigator.storage.estimate");
        script.Should().Contain("elapsedMilliseconds");
        script.Should().NotContain("species");
        script.Should().NotContain("latitude");
        script.Should().NotContain("base64,");
    }
}
