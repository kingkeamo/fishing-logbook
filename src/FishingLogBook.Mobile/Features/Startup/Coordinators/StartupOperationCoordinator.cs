namespace FishingLogBook.Mobile.Features.Startup.Coordinators;

public sealed class StartupOperationCoordinator
{
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private CancellationTokenSource? _currentCancellation;

    public async Task RestartAsync(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        CancellationTokenSource cancellation;
        lock (_stateLock)
        {
            _currentCancellation?.Cancel();
            cancellation = new CancellationTokenSource();
            _currentCancellation = cancellation;
        }

        await _operationGate.WaitAsync();
        try
        {
            lock (_stateLock)
            {
                if (!ReferenceEquals(_currentCancellation, cancellation))
                {
                    return;
                }
            }

            await operation(cancellation.Token);
        }
        finally
        {
            lock (_stateLock)
            {
                if (ReferenceEquals(_currentCancellation, cancellation))
                {
                    _currentCancellation = null;
                }
            }

            cancellation.Dispose();
            _operationGate.Release();
        }
    }

    public void Cancel()
    {
        lock (_stateLock)
        {
            var cancellation = _currentCancellation;
            _currentCancellation = null;
            cancellation?.Cancel();
        }
    }
}
