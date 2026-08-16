using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Diagnostics.Storage.DiagnosticProbeTests;

public class WhenTestingProbeScript
{
    [Fact]
    public void ItShouldNotCreateTheProductionDiagnosticDatabase()
    {
        // Arrange
        var script = ReadProbeScript();

        // Act
        var withoutIsolatedName = script.Replace(
            "FishingLogBookDiagnosticsTest",
            string.Empty,
            StringComparison.Ordinal);

        // Assert
        script.Should().Contain("FishingLogBookDiagnosticsTest");
        script.Should().Contain("probeEvents");
        withoutIsolatedName.Should().NotContain("FishingLogBookDiagnostics");
        script.Should().NotContain("diagnosticEvents");
        script.Should().NotContain("createIndex");
    }

    private static string ReadProbeScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "FishingLogBook.Web",
                "wwwroot",
                "js",
                "diagnostic-probe.js");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find wwwroot file 'js/diagnostic-probe.js'.");
    }
}
