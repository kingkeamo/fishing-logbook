using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.TestCatch.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Clients.TestCatchClientTests;

public class WhenTestingHttpClients
{
    [Fact]
    public async Task ItShouldCallTheAuthorizedApiClientWhenListingCatches()
    {
        // Arrange
        var apiHandler = new RecordingHandler("""[]""");
        var anonymousHandler = new RecordingHandler("""ok""");
        var client = CreateClient(apiHandler, anonymousHandler);

        // Act
        var result = await client.GetAllAsync(CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/test-catches");
        anonymousHandler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotAttachABearerTokenWhenUploadingToObjectStorage()
    {
        // Arrange
        var apiHandler = new RecordingHandler("""ok""");
        var anonymousHandler = new RecordingHandler("""ok""");
        var client = CreateClient(apiHandler, anonymousHandler);
        var uploadUrl = "https://storage.example.test/photos/upload";

        // Act
        await client.UploadPhotographAsync(uploadUrl, [1, 2, 3], "image/jpeg", CancellationToken.None);

        // Assert
        apiHandler.LastRequest.Should().BeNull();
        anonymousHandler.LastRequest.Should().NotBeNull();
        anonymousHandler.LastRequest!.RequestUri.Should().Be(new Uri(uploadUrl));
        anonymousHandler.LastRequest.Headers.Authorization.Should().BeNull();
        anonymousHandler.LastRequest.Content!.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("image/jpeg"));
    }

    private static TestCatchClient CreateClient(RecordingHandler apiHandler, RecordingHandler anonymousHandler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi)
            .Returns(new HttpClient(apiHandler) { BaseAddress = new Uri("https://api.test/") });
        factory.CreateClient(HttpClientNames.Anonymous)
            .Returns(new HttpClient(anonymousHandler));
        return new TestCatchClient(factory);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public RecordingHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
