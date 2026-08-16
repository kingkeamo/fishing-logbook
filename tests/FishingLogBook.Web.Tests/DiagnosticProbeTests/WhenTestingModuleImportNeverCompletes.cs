using AwesomeAssertions;
using FishingLogBook.Web.Diagnostics;

namespace FishingLogBook.Web.Tests.DiagnosticProbeTests;

public class WhenTestingModuleImportNeverCompletes : BaseDiagnosticIndexedDbProbeTest
{
    [Fact]
    public async Task ItShouldReportImportAsTheFailedStage()
    {
        // Arrange
        var sut = CreateSut(new HangingImportJsRuntime(), 250);

        // Act
        var result = await sut.RunIsolatedAsync(CancellationToken.None);

        // Assert
        result.DatabaseName.Should().Be(BrowserDiagnosticIndexedDbProbe.IsolatedDatabaseName);
        result.Succeeded.Should().BeFalse();
        result.FailedStage.Should().Be(BrowserDiagnosticIndexedDbProbe.StageStartingImport);
        result.LastCompletedStage.Should().BeNull();
        result.Error.Should().Contain(nameof(TaskCanceledException));
    }
}
