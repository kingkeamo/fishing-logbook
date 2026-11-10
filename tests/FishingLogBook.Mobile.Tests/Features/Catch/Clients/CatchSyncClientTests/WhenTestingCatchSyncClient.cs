using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Clients.CatchSyncClientTests;

public class WhenTestingCatchSyncClient : BaseAuthorizedApiClientTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PhotographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task ItShouldReportAServerFailureWhenUpsertingACatch()
    {
        // Arrange
        var (api, server) = CreateSut(_ => Json("{}", HttpStatusCode.ServiceUnavailable));
        var sut = new CatchSyncClient(api);

        // Act
        var result = await sut.UpsertAsync(new CatchDto(CatchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), [new CatchPhotographDto(PhotographId, CatchId, "image/jpeg")]), CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.ServerError);
        server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldPostTheCatchToTheCatchesEndpointWithTheAccessToken()
    {
        // Arrange
        string? body = null;
        var (api, server) = CreateSut(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json($$"""{"id":"{{CatchId}}","caughtOn":"2026-08-17T08:00:00+00:00"}""");
        });
        var sut = new CatchSyncClient(api);

        // Act
        var result = await sut.UpsertAsync(new CatchDto(CatchId, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), [new CatchPhotographDto(PhotographId, CatchId, "image/jpeg")]), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(CatchId);
        body.Should().Contain(CatchId.ToString("D"));
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/catches"));
        request.Authorization.Should().Be("Bearer " + AccessToken);
    }

    [Fact]
    public async Task ItShouldRequestAnUploadUrlForTheCatchPhotograph()
    {
        // Arrange
        var (api, server) = CreateSut(_ => Json("""{"objectKey":"catches/key.jpg","uploadUrl":"https://storage.test/upload"}"""));
        var sut = new CatchSyncClient(api);

        // Act
        var result = await sut.CreatePhotographUploadAsync(
            CatchId,
            new PhotographUploadRequestDto(PhotographId, "image/jpeg"),
            CancellationToken.None);

        // Assert
        result.Value!.ObjectKey.Should().Be("catches/key.jpg");
        result.Value.UploadUrl.Should().Be("https://storage.test/upload");
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be(new Uri($"https://api.example.com/api/catches/{CatchId:D}/photographs/upload-url"));
    }

    [Fact]
    public async Task ItShouldRecordAnUploadedPhotographWithoutExpectingABody()
    {
        // Arrange
        var (api, server) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = new CatchSyncClient(api);

        // Act
        var result = await sut.RecordPhotographAsync(
            CatchId,
            new RecordPhotographDto(PhotographId, "catches/key.jpg", "image/jpeg"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be(new Uri($"https://api.example.com/api/catches/{CatchId:D}/photographs"));
    }

    [Fact]
    public async Task ItShouldDeleteACatchPhotograph()
    {
        // Arrange
        var (api, server) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = new CatchSyncClient(api);

        // Act
        var result = await sut.DeletePhotographAsync(CatchId, PhotographId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Delete);
        request.Uri.Should().Be(new Uri($"https://api.example.com/api/catches/{CatchId:D}/photographs/{PhotographId:D}"));
    }

    [Fact]
    public async Task ItShouldReportANotFoundWhenDeletingAMissingPhotograph()
    {
        // Arrange
        var (api, server) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = new CatchSyncClient(api);

        // Act
        var result = await sut.DeletePhotographAsync(CatchId, PhotographId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ApiOutcomeEnum.NotFound);
        server.Requests.Should().ContainSingle();
    }
}
