using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripClientTests;

public class WhenTestingPhotographs : BaseTripClientTest
{
    private static readonly Guid PhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task ItShouldThrowWhenTheUploadUrlIsRefused()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        // Act
        var act = async () => await client.CreatePhotographUploadAsync(
            TripId,
            new PhotographUploadRequestDto(PhotographId, "image/jpeg"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be($"https://api.test/api/trips/{TripId:D}/photographs/upload-url");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheUploadUrlIsMissingFromTheResponse()
    {
        // Arrange
        var client = CreateClient(new RecordingHandler(HttpStatusCode.OK, "null"));

        // Act
        var act = async () => await client.CreatePhotographUploadAsync(
            TripId,
            new PhotographUploadRequestDto(PhotographId, "image/jpeg"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ItShouldThrowWhenRecordingThePhotographIsRejected()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.BadRequest);
        var client = CreateClient(handler);

        // Act
        var act = async () => await client.RecordPhotographAsync(
            TripId,
            new RecordTripPhotographDto(PhotographId, "trips/key", "image/jpeg", StartedOn),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ItShouldPostToTheTripUploadUrlRoute()
    {
        // Arrange
        var body = $$"""{"objectKey":"trips/a/b/c","uploadUrl":"https://storage.test/put"}""";
        var handler = new RecordingHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        // Act
        var upload = await client.CreatePhotographUploadAsync(
            TripId,
            new PhotographUploadRequestDto(PhotographId, "image/jpeg"),
            CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString()
            .Should().Be($"https://api.test/api/trips/{TripId:D}/photographs/upload-url");
        handler.LastBody.Should().Contain($"\"{PhotographId:D}\"");
        handler.LastBody.Should().Contain("image/jpeg");
        upload.ObjectKey.Should().Be("trips/a/b/c");
        upload.UploadUrl.Should().Be("https://storage.test/put");
    }

    [Fact]
    public async Task ItShouldPostTheRecordToTheTripPhotographsRoute()
    {
        // Arrange
        var capturedOn = StartedOn.AddMinutes(20);
        var addedOn = StartedOn.AddHours(2);
        var body = $$"""
            {
                "id": "{{PhotographId:D}}",
                "tripId": "{{TripId:D}}",
                "objectKey": "trips/a/b/c",
                "contentType": "image/jpeg",
                "addedOn": "{{addedOn:O}}",
                "capturedOn": "{{capturedOn:O}}"
            }
            """;
        var handler = new RecordingHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        // Act
        var recorded = await client.RecordPhotographAsync(
            TripId,
            new RecordTripPhotographDto(
                PhotographId,
                "trips/a/b/c",
                "image/jpeg",
                addedOn,
                capturedOn),
            CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString()
            .Should().Be($"https://api.test/api/trips/{TripId:D}/photographs");
        handler.LastBody.Should().Contain("trips/a/b/c");
        recorded!.Id.Should().Be(PhotographId);
        recorded.TripId.Should().Be(TripId);
        recorded.CapturedOn.Should().Be(capturedOn);
        recorded.AddedOn.Should().Be(addedOn);
    }

    [Fact]
    public async Task ItShouldPutTheBytesToThePresignedUrlWithTheContentType()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var client = CreateClient(handler);

        // Act
        await client.UploadPhotographAsync(
            "https://storage.test/put",
            [1, 2, 3],
            "image/jpeg",
            CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://storage.test/put");
        handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task ItShouldDeleteTheTripPhotographRoute()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        // Act
        await client.DeletePhotographAsync(TripId, PhotographId, CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.ToString()
            .Should().Be($"https://api.test/api/trips/{TripId:D}/photographs/{PhotographId:D}");
    }
}
