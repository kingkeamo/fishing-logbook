using System.Net;

namespace FishingLogBook.Web.Common.Offline.Synchronisers;

public static class SynchronisationFailureClassifier
{
    // 401 is deliberately excluded: the authorised HTTP pipeline (AuthorizationMessageHandler)
    // does not guarantee a 401 reaching a synchroniser means the rejection is permanent - it can
    // reflect a since-expired/revoked access token that a fresh sign-in resolves, so one stale
    // token must not permanently strand offline data. Treat it as Transient/WaitingToSynchronise.
    private static readonly HttpStatusCode[] PermanentStatusCodes =
    [
        HttpStatusCode.BadRequest,
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
