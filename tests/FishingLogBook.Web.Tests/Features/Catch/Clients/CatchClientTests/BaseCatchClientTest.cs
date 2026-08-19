using System.Net;
using System.Text;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Clients.CatchClientTests;

public class BaseCatchClientTest
{
    protected static CatchClient CreateClient(
        RecordingHandler apiHandler,
        RecordingHandler? anonymousHandler = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi)
            .Returns(new HttpClient(apiHandler) { BaseAddress = new Uri("https://api.test/") });
        factory.CreateClient(HttpClientNames.Anonymous)
            .Returns(new HttpClient(anonymousHandler ?? new RecordingHandler(HttpStatusCode.OK)));
        return new CatchClient(factory);
    }

    protected sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public RecordingHandler(HttpStatusCode statusCode, string responseBody = "")
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
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
