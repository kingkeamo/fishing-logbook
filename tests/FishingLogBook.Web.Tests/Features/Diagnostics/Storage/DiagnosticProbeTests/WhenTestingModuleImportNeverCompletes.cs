using AwesomeAssertions;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Storage.DiagnosticProbeTests;

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
        result.DatabaseName.Should().Be(DiagnosticIndexedDbProbe.IsolatedDatabaseName);
        result.Succeeded.Should().BeFalse();
        result.FailedStage.Should().Be(DiagnosticIndexedDbProbe.StageStartingImport);
        result.LastCompletedStage.Should().BeNull();
        result.Error.Should().Contain(nameof(TaskCanceledException));
    }
}
