using System.Net;
using System.Text;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Locations.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Locations.Clients.LocationLookupClientTests;

public class BaseLocationLookupClientTest
{
    protected static LocationLookupClient CreateClient(RecordingHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi)
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        return new LocationLookupClient(factory);
    }

    protected sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public RecordingHandler(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _body = body;
            _statusCode = statusCode;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
