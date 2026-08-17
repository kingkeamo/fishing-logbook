using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Users.Services;

public interface ICurrentUserClient
{
    Task<CurrentUserDto> GetCurrentAsync(CancellationToken cancellationToken);
}
