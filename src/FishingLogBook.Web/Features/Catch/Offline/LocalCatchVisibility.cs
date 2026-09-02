using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Offline;

internal static class LocalCatchVisibility
{
    public static IReadOnlyList<CatchModel> ForOwner(
        IReadOnlyList<CatchModel> records,
        Guid ownerUserId)
    {
        if (ownerUserId == Guid.Empty)
        {
            return [];
        }

        return records
            .Where(record => record.CaughtByUserId == ownerUserId || record.RecordedByUserId == ownerUserId)
            .ToArray();
    }
}
