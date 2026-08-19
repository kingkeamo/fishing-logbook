using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Users.Clients;

public interface ICurrentUserClient
{
    Task<CurrentUserDto> GetCurrentAsync(CancellationToken cancellationToken);
}
