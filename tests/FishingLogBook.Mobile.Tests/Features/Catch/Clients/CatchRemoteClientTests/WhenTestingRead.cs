using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Clients.CatchRemoteClientTests;

public sealed class WhenTestingRead : BaseAuthorizedApiClientTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldUseTheExistingAuthenticatedGetContractAndDeserializePhotos(bool list)
    {
        // Arrange
        var id = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var body = $$"""{"id":"{{id}}","caughtByUserId":"{{UserId}}","caughtOn":"2026-10-31T12:00:00Z","speciesName":"Pike","photographs":[{"id":"{{photoId}}","contentType":"image/jpeg","url":"https://photos.example/image"}]}""";
        var (api, server) = CreateSut(_ => Json(list ? "[" + body + "]" : body));
        var sut = new CatchRemoteClient(api);
        // Act
        var view = list ? (await sut.ListAsync(CancellationToken.None)).Value!.Single()
            : (await sut.GetAsync(id, CancellationToken.None)).Value!;
        // Assert
        view.Id.Should().Be(id);
        view.SpeciesName.Should().Be("Pike");
        view.Photographs.Single().Id.Should().Be(photoId);
        view.Photographs.Single().Url.Should().Be("https://photos.example/image");
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri!.AbsolutePath.Should().Be(list ? "/api/catches" : $"/api/catches/{id:D}");
        request.Uri.Query.Should().BeEmpty();
        request.Authorization.Should().Be("Bearer " + AccessToken);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ApiOutcomeEnum.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, ApiOutcomeEnum.SessionExpired)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ApiOutcomeEnum.ServerError)]
    public async Task ItShouldReturnTypedFailuresWithoutInventingAnEmptyCatch(HttpStatusCode status, ApiOutcomeEnum expected)
    {
        // Arrange
        var (api, _) = CreateSut(_ => new HttpResponseMessage(status));
        var sut = new CatchRemoteClient(api);
        // Act
        var result = await sut.GetAsync(Guid.NewGuid(), CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(expected);
        result.Value.Should().BeNull();
    }
}
