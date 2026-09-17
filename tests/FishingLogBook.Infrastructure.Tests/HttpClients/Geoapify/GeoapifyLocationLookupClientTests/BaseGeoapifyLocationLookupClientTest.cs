using System.Net;
using System.Text;
using FishingLogBook.Domain.Config;
using FishingLogBook.Infrastructure.HttpClients.Geoapify;
using Microsoft.Extensions.Options;

namespace FishingLogBook.Infrastructure.Tests.HttpClients.Geoapify.GeoapifyLocationLookupClientTests;

public class BaseGeoapifyLocationLookupClientTest
{
    protected static GeoapifyLocationLookupClient CreateClient(RecordingHandler handler, GeoapifyConfig? config = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://geoapify.test/")
        };
        return new GeoapifyLocationLookupClient(
            httpClient,
            Options.Create(config ?? new GeoapifyConfig
            {
                ApiKey = "test-key",
                BaseUrl = "https://geoapify.test/"
            }),
            TestMapper.Create());
    }

    protected static HttpResponseMessage JsonResponse(
        HttpRequestMessage request,
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    protected sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _response;
        private int _invocationCount;

        public RecordingHandler(
            Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
        {
            _response = response;
        }

        public int InvocationCount => _invocationCount;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return _response(Interlocked.Increment(ref _invocationCount), request, cancellationToken);
        }
    }
}
