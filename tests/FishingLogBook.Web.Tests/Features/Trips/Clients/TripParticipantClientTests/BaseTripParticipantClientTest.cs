using System.Net;
using System.Text;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Trips.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripParticipantClientTests;

public class BaseTripParticipantClientTest
{
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    protected static TripParticipantClient CreateClient(RecordingHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi)
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        return new TripParticipantClient(factory);
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

        public int Calls { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
