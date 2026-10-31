using FishingLogBook.Mobile.Features.Catch.Offline.Models;

namespace FishingLogBook.Mobile.Features.Catch.Offline.Stores;

public interface ICatchWorkingCopyStore
{
    Task<bool> TryCreateAsync(
        LocalCatchRecordModel record,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
