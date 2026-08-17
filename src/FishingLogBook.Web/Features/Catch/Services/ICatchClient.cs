using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Catch.Services;

public interface ICatchClient
{
    Task UpdateLocationVisibilityAsync(Guid catchId, string visibility, CancellationToken cancellationToken);
}
