namespace FishingLogBook.Web.Common.Offline.Synchronisers;

/// <summary>
/// Marks a failure that must always be treated as transient, regardless of any HTTP status
/// code it carries. Used for the raw object-storage PUT against a presigned URL, where a 4xx
/// (e.g. an expired or invalid signature) is not a permanent rejection - retrying the whole
/// upload sequence requests a fresh presigned URL and can very plausibly succeed.
/// </summary>
public sealed class TransientSynchronisationException : Exception
{
    public TransientSynchronisationException(Exception inner)
        : base(inner.Message, inner)
    {
    }
}
