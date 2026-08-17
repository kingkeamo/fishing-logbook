using System.Net;
using System.Text;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Profile.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Services.ProfileClientTests;

public class BaseProfileClientTest
{
    protected static ProfileClient CreateClient(
        RecordingHandler apiHandler,
        RecordingHandler? anonymousHandler = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi)
            .Returns(new HttpClient(apiHandler) { BaseAddress = new Uri("https://api.test/") });
        factory.CreateClient(HttpClientNames.Anonymous)
            .Returns(new HttpClient(anonymousHandler ?? new RecordingHandler("""ok""")));
        return new ProfileClient(factory);
    }

    protected static ProfileDto OwnProfile(Guid userId)
    {
        return new ProfileDto(
            userId,
            "Eamonn",
            null,
            null,
            null,
            "Westmeath",
            ["Coarse"],
            ["Pike"],
            true,
            false,
            true,
            true,
            false);
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

        public byte[]? LastBytes { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                LastBody = Encoding.UTF8.GetString(LastBytes);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
