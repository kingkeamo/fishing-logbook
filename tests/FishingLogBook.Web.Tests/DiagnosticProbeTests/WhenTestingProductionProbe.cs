using AwesomeAssertions;
using FishingLogBook.Web.Diagnostics;

namespace FishingLogBook.Web.Tests.DiagnosticProbeTests;

public class WhenTestingProductionProbe : BaseDiagnosticIndexedDbProbeTest
{
    [Fact]
    public async Task ItShouldCountTheProductionStore()
    {
        // Arrange
        var js = new RecordingProbeJsRuntime();
        var sut = CreateSut(js);

        // Act
        var result = await sut.RunAsync(
            BrowserDiagnosticIndexedDbProbe.ProductionDatabaseName,
            false,
            CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        js.DatabaseNames.Should().OnlyContain(name => name == BrowserDiagnosticIndexedDbProbe.ProductionDatabaseName);
        js.StoreNames.Should().OnlyContain(name => name == BrowserDiagnosticIndexedDbProbe.ProductionStoreName);
        js.StoreNames.Should().NotContain(BrowserDiagnosticIndexedDbProbe.IsolatedStoreName);
    }
}
