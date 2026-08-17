using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Offline;

internal static class LocalCatchVisibility
{
    public static IReadOnlyList<CatchModel> ForOwner(
        IReadOnlyList<CatchModel> records,
        Guid ownerUserId)
    {
        var hasForeignOwner = records.Any(record =>
            record.UserId != Guid.Empty && record.UserId != ownerUserId);
        return records
            .Where(record => IsVisibleTo(record, ownerUserId, hasForeignOwner))
            .ToArray();
    }

    private static bool IsVisibleTo(CatchModel record, Guid ownerUserId, bool hasForeignOwner)
    {
        if (record.UserId == ownerUserId)
        {
            return true;
        }

        return record.UserId == Guid.Empty && !hasForeignOwner;
    }
}
