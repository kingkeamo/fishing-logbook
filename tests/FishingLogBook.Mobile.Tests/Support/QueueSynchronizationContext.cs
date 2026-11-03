namespace FishingLogBook.Mobile.Tests.Support;

public sealed class QueueSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _work = [];
    private readonly TaskCompletionSource _posted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int PostCount { get; private set; }

    public bool IsDraining { get; private set; }

    public Task Posted => _posted.Task;

    public override void Post(SendOrPostCallback callback, object? state)
    {
        PostCount++;
        lock (_work)
        {
            _work.Enqueue((callback, state));
        }

        _posted.TrySetResult();
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        callback(state);
    }

    public void Drain()
    {
        IsDraining = true;
        try
        {
            while (TryDequeue(out var item))
            {
                var previous = Current;
                SetSynchronizationContext(this);
                try
                {
                    item.Callback(item.State);
                }
                finally
                {
                    SetSynchronizationContext(previous);
                }
            }
        }
        finally
        {
            IsDraining = false;
        }
    }

    private bool TryDequeue(out (SendOrPostCallback Callback, object? State) item)
    {
        lock (_work)
        {
            if (_work.Count == 0)
            {
                item = default;
                return false;
            }

            item = _work.Dequeue();
            return true;
        }
    }
}
