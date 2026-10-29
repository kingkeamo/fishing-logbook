using System.Text.Json;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Stores;

namespace FishingLogBook.Mobile.Native.Stores;

public sealed class SecureOfflineCapabilityStore : ISecureOfflineCapabilityStore
{
    private const string CapabilityKey = "offline.capability";

    public async Task<OfflineCapabilityModel?> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await SecureStorage.Default.GetAsync(CapabilityKey);
        cancellationToken.ThrowIfCancellationRequested();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : JsonSerializer.Deserialize<OfflineCapabilityModel>(value);
    }

    public async Task SaveAsync(OfflineCapabilityModel capability, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await SecureStorage.Default.SetAsync(CapabilityKey, JsonSerializer.Serialize(capability));
        cancellationToken.ThrowIfCancellationRequested();
    }

    public Task RemoveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(CapabilityKey);
        return Task.CompletedTask;
    }
}
