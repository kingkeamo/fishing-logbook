using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Catch.Clients.CatchClientTests;

public class WhenTestingGetAll : BaseCatchClientTest
{
    [Fact]
    public async Task ItShouldGetTheAuthorizedCatchesRoute()
    {
        // Arrange
        var views = new[]
        {
            new CatchViewDto(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.Parse("2026-08-17T12:00:00Z"))
            {
                SpeciesName = "Pike"
            }
        };
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(views, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var client = CreateClient(handler);

        // Act
        var actual = await client.GetAllAsync(CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/catches");
        actual.Should().ContainSingle(view => view.Id == views[0].Id && view.SpeciesName == "Pike");
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheResponseBodyIsNull()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, "null");
        var client = CreateClient(handler);

        // Act
        var actual = await client.GetAllAsync(CancellationToken.None);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldThrowWhenTheRequestFails()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var action = () => client.GetAllAsync(CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
    }
}
