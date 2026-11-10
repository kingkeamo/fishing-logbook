using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Catch.Clients.CatchClientTests;

public class WhenTestingCorrectAngler : BaseCatchClientTest
{
    private static readonly Guid CatchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid AnglerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ItShouldReportAMissingCatchWithoutAnErrorMessageWhenTheServerReturnsNotFound()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.NotFound, """{"errorMessage":"ignored"}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.CorrectAnglerAsync(CatchId, AnglerId, CancellationToken.None);

        // Assert
        result.Catch.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Patch);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/catches/{CatchId:D}/angler");
        handler.LastBody.Should().Contain(AnglerId.ToString());
    }

    [Fact]
    public async Task ItShouldReturnTheServersErrorMessageWhenTheCorrectionIsRejected()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.BadRequest, """{"errorMessage":"The angler is not on the trip."}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.CorrectAnglerAsync(CatchId, AnglerId, CancellationToken.None);

        // Assert
        result.Catch.Should().BeNull();
        result.ErrorMessage.Should().Be("The angler is not on the trip.");
    }

    [Fact]
    public async Task ItShouldReturnNoErrorMessageWhenARejectionHasNoBody()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.BadRequest, "null");
        var client = CreateClient(handler);

        // Act
        var result = await client.CorrectAnglerAsync(CatchId, AnglerId, CancellationToken.None);

        // Assert
        result.Catch.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnTheUpdatedCatchWhenTheCorrectionSucceeds()
    {
        // Arrange
        var updated = new CatchViewDto(CatchId, AnglerId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"))
        {
            AnglerName = "Mary",
            SpeciesName = "Pike"
        };
        var json = JsonSerializer.Serialize(updated, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var handler = new RecordingHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        // Act
        var result = await client.CorrectAnglerAsync(CatchId, AnglerId, CancellationToken.None);

        // Assert
        result.ErrorMessage.Should().BeNull();
        result.Catch.Should().NotBeNull();
        result.Catch!.Id.Should().Be(CatchId);
        result.Catch.CaughtByUserId.Should().Be(AnglerId);
        result.Catch.AnglerName.Should().Be("Mary");
    }

    [Fact]
    public async Task ItShouldRejectAMissingPhotographUploadUrl()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, "null");
        var client = CreateClient(handler);

        // Act
        var act = () => client.CreatePhotographUploadAsync(
            CatchId,
            new PhotographUploadRequestDto(Guid.NewGuid(), PhotographContentTypeConstants.Jpeg),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Photograph upload URL was missing.");
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be($"/api/catches/{CatchId:D}/photographs/upload-url");
    }
}
