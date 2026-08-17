using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.LocalCatchVisibilityTests;

public class BaseLocalCatchVisibilityTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected static CatchModel CatchFor(Guid catchId, Guid userId)
    {
        return new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg, [1])],
            UserId: userId);
    }
}
