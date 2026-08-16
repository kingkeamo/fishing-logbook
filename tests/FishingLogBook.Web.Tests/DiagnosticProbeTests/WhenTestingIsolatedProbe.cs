using AwesomeAssertions;
using FishingLogBook.Web.Diagnostics;

namespace FishingLogBook.Web.Tests.DiagnosticProbeTests;

public class WhenTestingIsolatedProbe : BaseDiagnosticIndexedDbProbeTest
{
    [Fact]
    public async Task ItShouldUseTheIsolatedDatabaseName()
    {
        // Arrange
        var js = new RecordingProbeJsRuntime();
        var sut = CreateSut(js);

        // Act
        var result = await sut.RunIsolatedAsync(CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.DatabaseName.Should().Be(BrowserDiagnosticIndexedDbProbe.IsolatedDatabaseName);
        result.LastCompletedStage.Should().Be(BrowserDiagnosticIndexedDbProbe.StageCountReturned);
        js.ImportPaths.Should().Equal("./js/diagnostic-probe.js");
        js.Invocations.Should().Equal("openProbeDatabase", "writeProbeRecord", "countProbeRecords");
        js.ImportPaths.Should().NotContain(path => path.Contains("diagnostic-store.js", StringComparison.Ordinal));
    }
}
