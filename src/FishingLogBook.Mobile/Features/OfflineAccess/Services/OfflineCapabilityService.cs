using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Stores;

namespace FishingLogBook.Mobile.Features.OfflineAccess.Services;

public sealed class OfflineCapabilityService : IOfflineCapabilityService
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly ISecureOfflineCapabilityStore _store;

    public OfflineCapabilityService(ISecureOfflineCapabilityStore store)
    {
        _store = store;
    }

    public async Task<OfflineCapabilityModel?> GetReadyAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var capability = await _store.GetAsync(cancellationToken);
            return IsValid(capability) && capability!.IsDeviceReady ? capability : null;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task MarkReadyAsync(string subject, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An offline capability requires an account owner.", nameof(userId));
        }

        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var capability = new OfflineCapabilityModel(subject, userId, IsDeviceReady: true);
            await _store.SaveAsync(capability, cancellationToken);
            var persisted = await _store.GetAsync(cancellationToken);
            if (persisted != capability)
            {
                throw new InvalidOperationException("The offline capability could not be verified after persistence.");
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task RevokeAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            await RevokeCoreAsync(cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task RevokeIfOwnedByAsync(
        string subject,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var capability = await _store.GetAsync(cancellationToken);
            if (!IsOwnedBy(capability, subject, userId))
            {
                return;
            }

            await RevokeCoreAsync(cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task RevokeIfOwnedByAnotherAccountAsync(
        string subject,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var capability = await _store.GetAsync(cancellationToken);
            if (capability is null || IsOwnedBy(capability, subject, userId))
            {
                return;
            }

            await RevokeCoreAsync(cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task RevokeCoreAsync(CancellationToken cancellationToken)
    {
        var capability = await _store.GetAsync(cancellationToken);
        Exception? persistenceFailure = null;
        if (capability is not null && capability.IsDeviceReady)
        {
            var disabled = capability with { IsDeviceReady = false };
            try
            {
                await _store.SaveAsync(disabled, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                persistenceFailure = exception;
            }
        }

        try
        {
            await _store.RemoveAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            persistenceFailure ??= exception;
        }

        var persisted = await _store.GetAsync(cancellationToken);
        if (persisted?.IsDeviceReady == true)
        {
            throw new InvalidOperationException(
                "The offline capability could not be revoked.",
                persistenceFailure);
        }
    }

    private static bool IsOwnedBy(OfflineCapabilityModel? capability, string subject, Guid userId)
    {
        return capability is not null &&
            string.Equals(capability.Subject, subject, StringComparison.Ordinal) &&
            capability.UserId == userId;
    }

    private static bool IsValid(OfflineCapabilityModel? capability)
    {
        return capability is not null &&
            !string.IsNullOrWhiteSpace(capability.Subject) &&
            capability.UserId != Guid.Empty;
    }
}
