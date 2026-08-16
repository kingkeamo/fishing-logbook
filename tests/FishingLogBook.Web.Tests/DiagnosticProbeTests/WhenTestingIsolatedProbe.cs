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
        var result = await sut.RunAsync(
            BrowserDiagnosticIndexedDbProbe.IsolatedDatabaseName,
            true,
            CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.LastCompletedStage.Should().Be(BrowserDiagnosticIndexedDbProbe.StageCountReturned);
        js.DatabaseNames.Should().OnlyContain(name => name == BrowserDiagnosticIndexedDbProbe.IsolatedDatabaseName);
        js.StoreNames.Should().OnlyContain(name => name == BrowserDiagnosticIndexedDbProbe.IsolatedStoreName);
        js.DatabaseNames.Should().NotContain("FishingLogBook");
    }
}
