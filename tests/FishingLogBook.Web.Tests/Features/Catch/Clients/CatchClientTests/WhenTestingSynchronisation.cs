using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Catch.Clients.CatchClientTests;

public class WhenTestingSynchronisation : BaseCatchClientTest
{
    [Fact]
    public async Task ItShouldPostCatchMetadata()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var client = CreateClient(handler);
        var catchId = Guid.NewGuid();
        var dto = new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T12:00:00Z"),
            [new CatchPhotographDto(Guid.NewGuid(), catchId, "image/jpeg")]);

        // Act
        await client.UpsertAsync(dto, CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/catches");
        var sent = JsonSerializer.Deserialize<CatchDto>(
            handler.LastBody!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        sent.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task ItShouldUploadPhotographBytesWithoutApiAuthorization()
    {
        // Arrange
        var apiHandler = new RecordingHandler(HttpStatusCode.OK);
        var storageHandler = new RecordingHandler(HttpStatusCode.OK);
        var client = CreateClient(apiHandler, storageHandler);

        // Act
        await client.UploadPhotographAsync(
            "https://storage.test/object",
            [1, 2, 3],
            "image/jpeg",
            CancellationToken.None);

        // Assert
        storageHandler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        storageHandler.LastRequest.RequestUri.Should().Be(new Uri("https://storage.test/object"));
        storageHandler.LastRequest.Headers.Authorization.Should().BeNull();
        storageHandler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        apiHandler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldUseTheProductionPhotographEndpoints()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var upload = new PhotographUploadDto("object-key", "https://storage.test/object");
        var uploadHandler = new RecordingHandler(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(upload));
        var client = CreateClient(uploadHandler);

        // Act
        var actual = await client.CreatePhotographUploadAsync(
            catchId,
            new PhotographUploadRequestDto(photographId, "image/jpeg"),
            CancellationToken.None);

        // Assert
        actual.Should().Be(upload);
        uploadHandler.LastRequest!.RequestUri!.PathAndQuery.Should().Be(
            $"/api/catches/{catchId:D}/photographs/upload-url");
    }

    [Fact]
    public async Task ItShouldRecordPhotographMetadata()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.NoContent);
        var client = CreateClient(handler);
        var catchId = Guid.NewGuid();
        var request = new RecordPhotographDto(
            Guid.NewGuid(),
            "object-key",
            "image/jpeg");

        // Act
        await client.RecordPhotographAsync(
            catchId,
            request,
            CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(
            $"/api/catches/{catchId:D}/photographs");
        var sent = JsonSerializer.Deserialize<RecordPhotographDto>(
            handler.LastBody!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        sent.Should().Be(request);
    }

    [Fact]
    public async Task ItShouldThrowWhenMetadataUpsertFails()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);
        var catchId = Guid.NewGuid();
        var dto = new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T12:00:00Z"),
            [new CatchPhotographDto(Guid.NewGuid(), catchId, "image/jpeg")]);

        // Act
        var action = () => client.UpsertAsync(dto, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/catches");
    }
}
