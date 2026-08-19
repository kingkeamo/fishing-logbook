namespace FishingLogBook.Web.Tests.JavaScript.OfflineStore;

public class BaseOfflineStoreTest
{
    protected static string ReadOfflineStoreScript()
    {
        return string.Concat(
            ReadWwwRootJs("browser", "timeout.js"),
            ReadWwwRootJs("storage", "indexed-db.js"),
            ReadWwwRootJs("storage", "offline-diagnostics.js"),
            ReadWwwRootJs("storage", "catch-store.js"));
    }

    private static string ReadWwwRootJs(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "FishingLogBook.Web", "wwwroot", "js" }
                    .Concat(relativeSegments)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find wwwroot file 'js/{string.Join('/', relativeSegments)}'.");
    }
}
