using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Users.Services.CurrentUserClientTests;

public class WhenTestingGetCurrent : BaseCurrentUserClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheResponseIsNotSuccessful()
    {
        // Arrange
        var apiHandler = new RecordingHandler(HttpStatusCode.Unauthorized, """{"title":"error"}""");
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.GetCurrentAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/users/current");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheBodyIsMissing()
    {
        // Arrange
        var apiHandler = new RecordingHandler(HttpStatusCode.OK, "null");
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.GetCurrentAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/users/current");
    }

    [Fact]
    public async Task ItShouldGetTheCurrentUserFromTheAuthorizedApi()
    {
        // Arrange
        var expected = new CurrentUserDto(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "owner@example.test");
        var json = JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var apiHandler = new RecordingHandler(HttpStatusCode.OK, json);
        var client = CreateClient(apiHandler);

        // Act
        var result = await client.GetCurrentAsync(CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/users/current");
        apiHandler.LastRequest.RequestUri.Query.Should().BeEmpty();
    }
}
