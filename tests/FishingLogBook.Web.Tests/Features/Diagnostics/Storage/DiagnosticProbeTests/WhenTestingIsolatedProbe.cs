using AwesomeAssertions;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Storage.DiagnosticProbeTests;

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
        result.DatabaseName.Should().Be(DiagnosticIndexedDbProbe.IsolatedDatabaseName);
        result.LastCompletedStage.Should().Be(DiagnosticIndexedDbProbe.StageCountReturned);
        js.ImportPaths.Should().Equal("./js/diagnostic-probe.js");
        js.Invocations.Should().Equal("openProbeDatabase", "writeProbeRecord", "countProbeRecords");
        js.ImportPaths.Should().NotContain(path => path.Contains("diagnostic-store.js", StringComparison.Ordinal));
    }
}
