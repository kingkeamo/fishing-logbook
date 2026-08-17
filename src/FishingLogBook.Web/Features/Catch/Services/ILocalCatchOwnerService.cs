namespace FishingLogBook.Web.Features.Catch.Services;

public interface ILocalCatchOwnerService
{
    Task<Guid> GetUserIdAsync(CancellationToken cancellationToken);
}
