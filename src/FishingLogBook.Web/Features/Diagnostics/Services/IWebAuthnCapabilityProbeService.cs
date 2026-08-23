using FishingLogBook.Web.Features.Diagnostics.Models;

namespace FishingLogBook.Web.Features.Diagnostics.Services;

public interface IWebAuthnCapabilityProbeService
{
    Task<bool> HasMetadataAsync(CancellationToken cancellationToken);

    Task<WebAuthnCapabilityProbeResultModel> GetStatusAsync(CancellationToken cancellationToken);

    Task<WebAuthnCapabilityProbeResultModel> ProvisionAsync(CancellationToken cancellationToken);

    Task<WebAuthnCapabilityProbeResultModel> VerifyOnlineAsync(CancellationToken cancellationToken);

    Task<WebAuthnCapabilityProbeResultModel> TestOfflineUnlockAsync(CancellationToken cancellationToken);

    Task RemoveMetadataAsync(CancellationToken cancellationToken);
}
