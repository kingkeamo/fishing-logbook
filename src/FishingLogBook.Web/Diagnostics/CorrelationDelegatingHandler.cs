using FishingLogBook.Shared.Diagnostics;

namespace FishingLogBook.Web.Diagnostics;

public sealed class CorrelationDelegatingHandler : DelegatingHandler
{
    private readonly CorrelationContext _correlationContext;

    public CorrelationDelegatingHandler(CorrelationContext correlationContext)
    {
        _correlationContext = correlationContext;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(CorrelationHeaders.CorrelationId);
        request.Headers.Add(CorrelationHeaders.CorrelationId, _correlationContext.CorrelationId.ToString("D"));
        return base.SendAsync(request, cancellationToken);
    }
}
