using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.ServiceWorkerTests;

public class BaseServiceWorkerTest
{
    protected static string ReadWwwRootFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "FishingLogBook.Web", "wwwroot", fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find wwwroot file '{fileName}'.");
    }

    protected static string ReadWebProjectFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "FishingLogBook.Web",
                "FishingLogBook.Web.csproj");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find FishingLogBook.Web.csproj.");
    }
}
