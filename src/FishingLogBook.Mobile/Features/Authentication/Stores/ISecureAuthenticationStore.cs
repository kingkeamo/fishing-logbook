using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Features.Authentication.Stores;

public interface ISecureAuthenticationStore
{
    Task<PersistedAuthenticationModel?> GetCredentialAsync();
    Task SaveCredentialAsync(PersistedAuthenticationModel credential);
    Task RemoveCredentialAsync();
    Task<PendingAuthorizationModel?> GetPendingAuthorizationAsync();
    Task<PendingAuthorizationModel?> ConsumePendingAuthorizationAsync();
    Task SavePendingAuthorizationAsync(PendingAuthorizationModel pendingAuthorization);
    Task RemovePendingAuthorizationAsync();
}
