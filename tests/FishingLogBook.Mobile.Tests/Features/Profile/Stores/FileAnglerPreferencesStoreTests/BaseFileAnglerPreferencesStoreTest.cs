using FishingLogBook.Mobile.Features.Profile.Stores;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Stores.FileAnglerPreferencesStoreTests;

public abstract class BaseFileAnglerPreferencesStoreTest : IDisposable
{
    protected static readonly Guid FirstUserId = Guid.Parse("5b1f0c6a-2d3e-4f50-8a71-9c2b3d4e5f60");
    protected static readonly Guid SecondUserId = Guid.Parse("e7d6c5b4-a392-4180-9f7e-6d5c4b3a2918");
    protected readonly string Directory = Path.Combine(Path.GetTempPath(), "flb-preferences-" + Guid.NewGuid().ToString("N"));
    protected readonly FileAnglerPreferencesStore Sut;

    protected BaseFileAnglerPreferencesStoreTest()
    {
        Sut = new FileAnglerPreferencesStore(Directory);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory))
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    protected string PathFor(Guid userId)
    {
        return Path.Combine(Directory, userId.ToString("N") + ".json");
    }
}
