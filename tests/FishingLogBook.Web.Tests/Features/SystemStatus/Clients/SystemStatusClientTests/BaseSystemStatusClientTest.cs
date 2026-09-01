using System.Net;
using System.Text;
using FishingLogBook.Web.Features.SystemStatus.Clients;

namespace FishingLogBook.Web.Tests.Features.SystemStatus.Clients.SystemStatusClientTests;

public class BaseSystemStatusClientTest
{
    protected static SystemStatusClient CreateClient(HttpMessageHandler handler)
    {
        return new SystemStatusClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
    }

    protected static SystemStatusClient CreateClient(RecordingHandler handler)
    {
        return CreateClient((HttpMessageHandler)handler);
    }

    protected sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
