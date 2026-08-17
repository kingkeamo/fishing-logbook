using System.Net;
using System.Text;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Users.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Users.Services.CurrentUserClientTests;

public class BaseCurrentUserClientTest
{
    protected static CurrentUserClient CreateClient(RecordingHandler apiHandler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi)
            .Returns(new HttpClient(apiHandler) { BaseAddress = new Uri("https://api.test/") });
        return new CurrentUserClient(factory);
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
