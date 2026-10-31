using FishingLogBook.Mobile.Features.Authentication.Constants;
using FishingLogBook.Mobile.Features.Photographs.Clients;
using FishingLogBook.Mobile.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Clients.PhotographUploadClientTests;

public abstract class BasePhotographUploadClientTest
{
    protected const string ValidUrl = "https://storage.example/catches/c/p?X-Signature=abc";
    protected const string ValidContentType = "image/jpeg";

    protected readonly IHttpClientFactory MockHttpClientFactory = Substitute.For<IHttpClientFactory>();
    protected readonly List<CapturedBody> Bodies = [];
    protected RecordingHttpMessageHandler Server = null!;
    protected PhotographUploadClient Sut = null!;

    protected void CreateSut(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        Server = new RecordingHttpMessageHandler(async (request, token) =>
        {
            Bodies.Add(new CapturedBody(
                request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(token),
                request.Content?.Headers.ContentType?.ToString()));
            return await respond(request, token);
        });
        MockHttpClientFactory.CreateClient(AuthenticationHttpClientConstants.Anonymous)
            .Returns(_ => new HttpClient(Server, disposeHandler: false));
        Sut = new PhotographUploadClient(MockHttpClientFactory, NullLogger<PhotographUploadClient>.Instance);
    }

    protected void CreateSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        CreateSut((request, _) => Task.FromResult(respond(request)));
    }

    protected sealed record CapturedBody(byte[]? Bytes, string? ContentType);
}
