using System.Net;

namespace FishingLogBook.Web.Common.Offline.Synchronisers;

public static class SynchronisationFailureClassifier
{
    private static readonly HttpStatusCode[] PermanentStatusCodes =
    [
        HttpStatusCode.BadRequest,
        HttpStatusCode.Unauthorized,
        HttpStatusCode.Forbidden,
        HttpStatusCode.NotFound,
        HttpStatusCode.Conflict
    ];

    public static SynchronisationFailureKind Classify(Exception exception)
    {
        if (exception is TransientSynchronisationException)
        {
            return SynchronisationFailureKind.Transient;
        }

        if (exception is HttpRequestException { StatusCode: { } statusCode }
            && PermanentStatusCodes.Contains(statusCode))
        {
            return SynchronisationFailureKind.Permanent;
        }

        return SynchronisationFailureKind.Transient;
    }
}
