using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Stores;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Services;

public sealed class OfflineCapabilityService(
    ISecureOfflineCapabilityStore store) : IOfflineCapabilityService
{
    public async Task<OfflineCapabilityModel?> GetReadyAsync(CancellationToken cancellationToken)
    {
        var capability = await store.GetAsync(cancellationToken);
        return IsValid(capability) && capability!.IsDeviceReady ? capability : null;
    }

    public async Task MarkReadyAsync(string subject, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An offline capability requires an account owner.", nameof(userId));
        }

        var capability = new OfflineCapabilityModel(subject, userId, IsDeviceReady: true);
        await store.SaveAsync(capability, cancellationToken);
        var persisted = await store.GetAsync(cancellationToken);
        if (persisted != capability)
        {
            throw new InvalidOperationException("The offline capability could not be verified after persistence.");
        }
    }

    public async Task RevokeAsync(CancellationToken cancellationToken)
    {
        var capability = await store.GetAsync(cancellationToken);
        Exception? persistenceFailure = null;
        if (capability is not null && capability.IsDeviceReady)
        {
            var disabled = capability with { IsDeviceReady = false };
            try
            {
                await store.SaveAsync(disabled, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                persistenceFailure = exception;
            }
        }

        try
        {
            await store.RemoveAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            persistenceFailure ??= exception;
        }

        var persisted = await store.GetAsync(cancellationToken);
        if (persisted?.IsDeviceReady == true)
        {
            throw new InvalidOperationException(
                "The offline capability could not be revoked.",
                persistenceFailure);
        }
    }

    public async Task RevokeIfOwnedByAnotherAccountAsync(
        string subject,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var capability = await store.GetAsync(cancellationToken);
        if (capability is null ||
            string.Equals(capability.Subject, subject, StringComparison.Ordinal) && capability.UserId == userId)
        {
            return;
        }

        await RevokeAsync(cancellationToken);
    }

    private static bool IsValid(OfflineCapabilityModel? capability)
    {
        return capability is not null &&
            !string.IsNullOrWhiteSpace(capability.Subject) &&
            capability.UserId != Guid.Empty;
    }
}
