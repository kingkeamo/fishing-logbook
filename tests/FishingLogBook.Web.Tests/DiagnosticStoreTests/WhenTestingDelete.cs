using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.DiagnosticStoreTests;

public class WhenTestingDelete
{
    [Fact]
    public void ItShouldDeleteByParsedJsonIds()
    {
        // Arrange
        var script = ReadDiagnosticStoreScript();

        // Act
        // Assert
        script.Should().Contain("JSON.parse(idsJson");
        script.Should().Contain("store.delete(key)");
    }

    private static string ReadDiagnosticStoreScript()
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
                "storage",
                "diagnostic-store.js");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find wwwroot file 'js/storage/diagnostic-store.js'.");
    }
}
