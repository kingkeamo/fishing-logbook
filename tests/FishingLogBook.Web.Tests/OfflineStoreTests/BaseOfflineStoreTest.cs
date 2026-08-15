namespace FishingLogBook.Web.Tests.OfflineStoreTests;

public class BaseOfflineStoreTest
{
    protected static string ReadOfflineStoreScript()
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
                "offline-store.js");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find wwwroot file 'js/offline-store.js'.");
    }
}
