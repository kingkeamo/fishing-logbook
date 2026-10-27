using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Clients.FishingLocationClientTests;

public class WhenTestingGet : BaseFishingLocationClientTest
{
    [Fact]
    public async Task ItShouldReportForbiddenWhenTheApiRefusesAccess()
    {
        // Arrange
        var (sut, server) = CreateClient(_ => Json(string.Empty, HttpStatusCode.Forbidden));
        // Act
        var result = await sut.GetAsync(CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Forbidden);
        server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldReadPreferredLocationsFromTheExistingEndpoint()
    {
        // Arrange
        var locationId = Guid.Parse("4d2b8c1e-7a8f-4e57-9d1c-2f0e6b3a9c55");
        var (sut, server) = CreateClient(_ => Json(
            $$"""{"locations":[{"id":"{{locationId}}","name":"Lough Corrib","isDefault":true}]}"""));
        // Act
        var result = await sut.GetAsync(CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Success);
        result.Value!.Locations.Should().Equal(new FishingLocationPreferenceDto(locationId, "Lough Corrib", true));
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/profiles/me/fishing-locations"));
        request.Authorization.Should().Be("Bearer " + AccessToken);
    }
}
