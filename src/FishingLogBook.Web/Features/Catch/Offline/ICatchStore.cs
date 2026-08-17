using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Offline;

public interface ICatchStore
{
    Task SaveAsync(CatchModel catchRecord, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatchModel>> GetAllAsync(Guid ownerUserId, CancellationToken cancellationToken);
}
