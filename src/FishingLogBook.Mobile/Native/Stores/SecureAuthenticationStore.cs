using System.Text.Json;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Stores;

namespace FishingLogBook.Mobile.Native.Stores;

public sealed class SecureAuthenticationStore : ISecureAuthenticationStore
{
    private const string CredentialKey = "authentication.credential";
    private const string PendingAuthorizationKey = "authentication.pending";

    public async Task<PersistedAuthenticationModel?> GetCredentialAsync()
    {
        var value = await SecureStorage.Default.GetAsync(CredentialKey);
        return Deserialize<PersistedAuthenticationModel>(value);
    }

    public Task SaveCredentialAsync(PersistedAuthenticationModel credential)
    {
        return SecureStorage.Default.SetAsync(CredentialKey, JsonSerializer.Serialize(credential));
    }

    public Task RemoveCredentialAsync()
    {
        SecureStorage.Default.Remove(CredentialKey);
        return Task.CompletedTask;
    }

    public async Task<PendingAuthorizationModel?> GetPendingAuthorizationAsync()
    {
        var value = await SecureStorage.Default.GetAsync(PendingAuthorizationKey);
        return Deserialize<PendingAuthorizationModel>(value);
    }

    public async Task<PendingAuthorizationModel?> ConsumePendingAuthorizationAsync()
    {
        var value = await SecureStorage.Default.GetAsync(PendingAuthorizationKey);
        SecureStorage.Default.Remove(PendingAuthorizationKey);
        return Deserialize<PendingAuthorizationModel>(value);
    }

    public Task SavePendingAuthorizationAsync(PendingAuthorizationModel pendingAuthorization)
    {
        return SecureStorage.Default.SetAsync(
            PendingAuthorizationKey,
            JsonSerializer.Serialize(pendingAuthorization));
    }

    public Task RemovePendingAuthorizationAsync()
    {
        SecureStorage.Default.Remove(PendingAuthorizationKey);
        return Task.CompletedTask;
    }

    private static T? Deserialize<T>(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? default : JsonSerializer.Deserialize<T>(value);
    }
}
