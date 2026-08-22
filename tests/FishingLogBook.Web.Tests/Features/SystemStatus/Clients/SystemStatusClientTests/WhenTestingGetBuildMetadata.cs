using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.SystemStatus.Clients.SystemStatusClientTests;

public class WhenTestingGetBuildMetadata : BaseSystemStatusClientTest
{
    [Fact]
    public async Task ItShouldRequestAndDeserialiseApiBuildMetadata()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {"version":"0.2.0","sha":"api5678","environment":"prod","builtOn":"2026-08-22T00:00:00Z"}
            """);

        var result = await CreateClient(handler).GetBuildMetadataAsync(CancellationToken.None);

        result!.Version.Should().Be("0.2.0");
        result.Sha.Should().Be("api5678");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/system/build");
    }

    [Fact]
    public async Task ItShouldThrowWhenApiBuildMetadataFails()
    {
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable, "{}");

        var act = () => CreateClient(handler).GetBuildMetadataAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
