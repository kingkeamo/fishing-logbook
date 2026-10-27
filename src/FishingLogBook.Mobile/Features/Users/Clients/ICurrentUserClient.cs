using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Users.Clients;

public interface ICurrentUserClient
{
    Task<CurrentUserDto> GetCurrentAsync(string accessToken, CancellationToken cancellationToken);
}
