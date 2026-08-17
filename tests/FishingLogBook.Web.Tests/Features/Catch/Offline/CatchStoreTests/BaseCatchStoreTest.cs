namespace FishingLogBook.Web.Tests.Features.Catch.Offline.CatchStoreTests;

public class BaseCatchStoreTest
{
    protected readonly Dictionary<Guid, FishingLogBook.Web.Features.Catch.Models.CatchModel> BackingCatches = [];
    protected readonly Dictionary<Guid, byte[]> BackingPhotographs = [];
    protected readonly MemoryCatchStore Sut;

    protected BaseCatchStoreTest()
    {
        Sut = new MemoryCatchStore(BackingCatches, BackingPhotographs);
    }
}
