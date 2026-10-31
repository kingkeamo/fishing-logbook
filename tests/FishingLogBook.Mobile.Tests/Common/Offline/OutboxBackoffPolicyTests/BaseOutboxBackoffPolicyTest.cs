using FishingLogBook.Mobile.Common.Offline.Services;

namespace FishingLogBook.Mobile.Tests.Common.Offline.OutboxBackoffPolicyTests;

public abstract class BaseOutboxBackoffPolicyTest
{
    protected const int MaximumAttempts = 5;
    protected static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(10);
    protected static readonly TimeSpan MaximumDelay = TimeSpan.FromSeconds(100);
    protected static readonly TimeSpan Precision = TimeSpan.FromMilliseconds(1);

    protected static OutboxBackoffPolicy CreatePolicy(double random)
    {
        return new OutboxBackoffPolicy(BaseDelay, MaximumDelay, MaximumAttempts, () => random);
    }
}
