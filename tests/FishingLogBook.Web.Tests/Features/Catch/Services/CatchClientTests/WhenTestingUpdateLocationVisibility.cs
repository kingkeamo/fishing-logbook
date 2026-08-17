using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.CatchClientTests;

public class WhenTestingUpdateLocationVisibility : BaseCatchClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheResponseIsNotSuccessful()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var apiHandler = new RecordingHandler(HttpStatusCode.Forbidden, """{"title":"error"}""");
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.UpdateLocationVisibilityAsync(
            catchId,
            LocationDefaults.Public,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Patch);
        apiHandler.LastRequest.RequestUri!.PathAndQuery
            .Should()
            .Be($"/api/catches/{catchId:D}/location-visibility");
    }

    [Fact]
    public async Task ItShouldIgnoreNotFound()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var apiHandler = new RecordingHandler(HttpStatusCode.NotFound);
        var client = CreateClient(apiHandler);

        // Act
        var act = () => client.UpdateLocationVisibilityAsync(
            catchId,
            LocationDefaults.Approximate,
            CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Patch);
        apiHandler.LastRequest.RequestUri!.PathAndQuery
            .Should()
            .Be($"/api/catches/{catchId:D}/location-visibility");
        var sent = JsonSerializer.Deserialize<UpdateCatchLocationVisibilityDto>(
            apiHandler.LastBody!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        sent.Should().NotBeNull();
        sent!.Visibility.Should().Be(LocationDefaults.Approximate);
    }

    [Fact]
    public async Task ItShouldPatchVisibilityForTheCatch()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var apiHandler = new RecordingHandler(HttpStatusCode.NoContent);
        var client = CreateClient(apiHandler);

        // Act
        await client.UpdateLocationVisibilityAsync(
            catchId,
            LocationDefaults.Public,
            CancellationToken.None);

        // Assert
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Patch);
        apiHandler.LastRequest.RequestUri!.PathAndQuery
            .Should()
            .Be($"/api/catches/{catchId:D}/location-visibility");
        var sent = JsonSerializer.Deserialize<UpdateCatchLocationVisibilityDto>(
            apiHandler.LastBody!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        sent.Should().NotBeNull();
        sent!.Visibility.Should().Be(LocationDefaults.Public);
        apiHandler.LastBody.Should().NotContain("userId");
        apiHandler.LastBody.Should().NotContain("latitude");
    }
}
