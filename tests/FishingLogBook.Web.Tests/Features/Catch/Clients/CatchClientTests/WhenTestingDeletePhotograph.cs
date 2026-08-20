using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Catch.Clients.CatchClientTests;

public class WhenTestingDeletePhotograph : BaseCatchClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheResponseIsNotSuccessful()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var apiHandler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.DeletePhotographAsync(catchId, photographId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        apiHandler.LastRequest.RequestUri!.PathAndQuery
            .Should()
            .Be($"/api/catches/{catchId:D}/photographs/{photographId:D}");
    }

    [Fact]
    public async Task ItShouldTreatNotFoundAsAnAlreadyDeletedPhotograph()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var apiHandler = new RecordingHandler(HttpStatusCode.NotFound);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.DeletePhotographAsync(catchId, photographId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ItShouldDeleteThePhotograph()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var apiHandler = new RecordingHandler(HttpStatusCode.NoContent);
        var client = CreateClient(apiHandler);

        // Act
        await client.DeletePhotographAsync(catchId, photographId, CancellationToken.None);

        // Assert
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        apiHandler.LastRequest.RequestUri!.PathAndQuery
            .Should()
            .Be($"/api/catches/{catchId:D}/photographs/{photographId:D}");
    }
}
