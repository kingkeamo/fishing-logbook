using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.CatchStoreTests;

public class BaseCatchStoreTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected readonly Dictionary<Guid, CatchModel> BackingCatches = [];
    protected readonly Dictionary<Guid, byte[]> BackingPhotographs = [];
    protected readonly MemoryCatchStore Sut;

    protected BaseCatchStoreTest()
    {
        Sut = new MemoryCatchStore(BackingCatches, BackingPhotographs);
    }
}
