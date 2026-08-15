using FishingLogBook.Web.Offline;

namespace FishingLogBook.Web.Tests.TestCatchStoreTests;

public class BaseTestCatchStoreTest
{
    protected readonly List<string> BackingStore = [];
    protected readonly TestCatchStore Sut;

    protected BaseTestCatchStoreTest()
    {
        Sut = new TestCatchStore(new MemoryTestCatchJsonStore(BackingStore));
    }
}
