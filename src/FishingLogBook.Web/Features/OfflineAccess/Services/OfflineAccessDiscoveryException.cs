namespace FishingLogBook.Web.Features.OfflineAccess.Services;

public sealed class OfflineAccessDiscoveryException : Exception
{
    public OfflineAccessDiscoveryException(string detail)
        : base(detail)
    {
    }
}
