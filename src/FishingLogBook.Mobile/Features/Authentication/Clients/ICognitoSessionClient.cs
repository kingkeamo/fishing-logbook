namespace FishingLogBook.Mobile.Features.Authentication.Clients;

public interface ICognitoSessionClient
{
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task OpenLogoutAsync(CancellationToken cancellationToken);
}
