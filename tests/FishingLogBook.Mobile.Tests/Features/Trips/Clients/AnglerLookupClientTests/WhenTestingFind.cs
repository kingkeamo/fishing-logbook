using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Clients.AnglerLookupClientTests;

public sealed class WhenTestingFind : BaseAuthorizedApiClientTest
{
    [Fact]
    public async Task ItShouldEscapeTheQueryAndDeserializeTheMatchingAnglers()
    {
        // Arrange
        var friendId = Guid.NewGuid();
        var json = $$"""[{"userId":"{{friendId}}","displayName":"Sam O'Neill","photographUrl":null,"homeRegion":"Galway","email":null}]""";
        var (api, server) = CreateSut(_ => Json(json));
        var sut = new AnglerLookupClient(api);
        // Act
        var result = await sut.FindAsync("sam & co", CancellationToken.None);
        // Assert
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri!.AbsolutePath.Should().Be("/api/profiles/lookup");
        request.Uri.Query.Should().Be("?q=sam%20%26%20co");
        result.Value.Should().ContainSingle(angler => angler.UserId == friendId && angler.DisplayName == "Sam O'Neill");
    }
}
