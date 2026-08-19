using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Offline.Stores;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Offline.TestCatchStoreTests;

public class BaseTestCatchStoreTest
{
    protected readonly List<string> BackingStore = [];
    protected readonly TestCatchStore Sut;

    protected BaseTestCatchStoreTest()
    {
        Sut = new TestCatchStore(new MemoryTestCatchJsonStore(BackingStore));
    }
}
