using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Dashboard.Clients;
using FishingLogBook.Web.Tests.Features.Trips.Clients.TripClientTests;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Dashboard.Clients.DashboardClientTests;

public class WhenTestingGet : BaseTripClientTest
{
    [Fact]
    public async Task ItShouldUseTheAuthorizedNamedClientAndDeserializeEverySection()
    {
        // Arrange
        var winner = Guid.NewGuid();
        var data = new DashboardDto
        {
            Lifetime = new DashboardTotalsDto { Catches = 10, Trips = 3, Species = 1 },
            AllTimePersonalBests = [new DashboardPersonalBestDto { CatchId = winner, Weight = 12.4m, SpeciesName = "Pike" }],
            CatchStatistics = [new DashboardCatchCountDto { Count = 3 }],
            FilteredPersonalBests = [new DashboardPersonalBestDto { CatchId = winner, Weight = 12.4m }],
            Species = ["Pike"],
            Methods = ["Lure"],
            Venues = ["Lake"]
        };
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonSerializer.Serialize(data));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi).Returns(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        var sut = new DashboardClient(factory);

        // Act
        var result = await sut.GetAsync(new DashboardFilterDto { Species = "Sea trout", Method = "Fly", Venue = "Lake & river" }, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(data);
        handler.Calls.Should().Be(1);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/dashboard?species=Sea%20trout&method=Fly&venue=Lake%20%26%20river");
        handler.LastBody.Should().BeNull();
        factory.Received(1).CreateClient(HttpClientNames.AuthorizedApi);
        factory.DidNotReceive().CreateClient(HttpClientNames.Anonymous);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.Unauthorized, "{}")]
    [InlineData(HttpStatusCode.OK, "null")]
    public async Task ItShouldRejectFailuresAndMissingData(HttpStatusCode status, string body)
    {
        // Arrange
        var handler = new RecordingHandler(status, body);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi).Returns(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        var sut = new DashboardClient(factory);

        // Act
        var action = () => sut.GetAsync(new DashboardFilterDto(), CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
        handler.Calls.Should().Be(1);
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/dashboard");
        factory.Received(1).CreateClient(HttpClientNames.AuthorizedApi);
    }
}
