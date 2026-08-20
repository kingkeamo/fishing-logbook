using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Catch.Clients.CatchClientTests;

public class WhenTestingGet : BaseCatchClientTest
{
    [Fact]
    public async Task ItShouldGetTheCatchByRoute()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var view = new CatchViewDto(catchId, Guid.NewGuid(), DateTimeOffset.Parse("2026-08-17T12:00:00Z"))
        {
            SpeciesName = "Pike"
        };
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(view, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var client = CreateClient(handler);

        // Act
        var actual = await client.GetAsync(catchId, CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/catches/{catchId:D}");
        actual.Should().NotBeNull();
        actual!.Id.Should().Be(catchId);
        actual.SpeciesName.Should().Be("Pike");
    }

    [Fact]
    public async Task ItShouldReturnNullWhenTheCatchIsNotFound()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var handler = new RecordingHandler(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        // Act
        var actual = await client.GetAsync(catchId, CancellationToken.None);

        // Assert
        actual.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldThrowWhenTheRequestFails()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var action = () => client.GetAsync(catchId, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
    }
}
