using System.Net;
using System.Text;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Profile.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.FishingLocationClientTests;

public class BaseFishingLocationClientTest
{
    protected static readonly Guid CorribId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid MoyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    protected static FishingLocationClient CreateClient(RecordingHandler apiHandler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi)
            .Returns(new HttpClient(apiHandler) { BaseAddress = new Uri("https://api.test/") });
        return new FishingLocationClient(factory);
    }

    protected sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
