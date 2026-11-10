using System.Net;
using System.Text;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.OfflineAccess.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Clients.OfflineAccessPreferenceClientTests;

public class WhenTestingPreference
{
    private const string Path = "/api/users/current/offline-access-preference";

    private static OfflineAccessPreferenceClient CreateClient(RecordingHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi)
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        return new OfflineAccessPreferenceClient(factory);
    }

    [Fact]
    public async Task ItShouldReadThePreferenceFromTheAuthorizedApi()
    {
        // Arrange
        var handler = new RecordingHandler("""{"enabled":true,"enabledAt":"2026-08-17T08:00:00+00:00"}""");
        var client = CreateClient(handler);

        // Act
        var preference = await client.GetAsync(CancellationToken.None);

        // Assert
        preference.Enabled.Should().BeTrue();
        preference.EnabledAt.Should().Be(DateTimeOffset.Parse("2026-08-17T08:00:00Z"));
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(Path);
    }

    [Fact]
    public async Task ItShouldRejectAMissingPreferenceBodyWhenReading()
    {
        // Arrange
        var handler = new RecordingHandler("null");
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Offline access preference was missing.");
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be(Path);
    }

    [Fact]
    public async Task ItShouldSendTheChosenPreferenceAndReturnTheSavedOne()
    {
        // Arrange
        var handler = new RecordingHandler("""{"enabled":true}""");
        var client = CreateClient(handler);

        // Act
        var saved = await client.SetAsync(true, CancellationToken.None);

        // Assert
        saved.Enabled.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(Path);
        handler.LastBody.Should().Contain("\"enabled\":true");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheServerRejectsTheChange()
    {
        // Arrange
        var handler = new RecordingHandler("""{"title":"error"}""", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var act = () => client.SetAsync(false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastBody.Should().Contain("\"enabled\":false");
    }

    [Fact]
    public async Task ItShouldRejectAMissingPreferenceBodyWhenSaving()
    {
        // Arrange
        var handler = new RecordingHandler("null");
        var client = CreateClient(handler);

        // Act
        var act = () => client.SetAsync(true, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Offline access preference was missing.");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
    }

    private sealed class RecordingHandler : HttpMessageHandler
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
