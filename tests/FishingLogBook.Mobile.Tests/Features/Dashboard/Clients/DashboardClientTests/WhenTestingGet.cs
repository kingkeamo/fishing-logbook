using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Dashboard.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Dashboard.Clients.DashboardClientTests;

public class WhenTestingGet : BaseAuthorizedApiClientTest
{
    [Theory]
    [InlineData(HttpStatusCode.OK, ApiOutcomeEnum.Success)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ApiOutcomeEnum.ServerError)]
    [InlineData(HttpStatusCode.Unauthorized, ApiOutcomeEnum.SessionExpired)]
    public async Task ItShouldUseTheAuthorizedPipelineAndReadTheDashboardContract(HttpStatusCode status, ApiOutcomeEnum expected)
    {
        // Arrange
        var data = new DashboardDto { Lifetime = new DashboardTotalsDto { Catches = 8, Trips = 2, Species = 3 } };
        var (api, server) = CreateSut(_ => Json(JsonSerializer.Serialize(data), status));
        var sut = new DashboardClient(api);

        // Act
        var result = await sut.GetAsync(new DashboardFilterDto { Species = "Sea trout", Method = "Fly", Venue = "Lake & river" }, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(expected);
        if (result.IsSuccess)
        {
            result.Value.Should().BeEquivalentTo(data);
        }
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri!.PathAndQuery.Should().Be("/api/dashboard?species=Sea%20trout&method=Fly&venue=Lake%20%26%20river");
        request.Authorization.Should().Be("Bearer " + AccessToken);
    }
}
