using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;

namespace FishingLogBook.Mobile.Tests.Features.About.Clients.SystemStatusClientTests;

public sealed class WhenTestingStatus : BaseSystemStatusClientTest
{
    [Fact]
    public async Task ItShouldReadApiHealthFromTheExistingHealthEndpoint()
    {
        // Arrange
        var (sut, server) = CreateClient(_ => Json("""{"status":"Healthy"}"""));

        // Act
        var result = await sut.GetApiHealthAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Healthy");
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.Should().Be(new Uri("https://api.example.com/health"));
    }

    [Fact]
    public async Task ItShouldReadDatabaseStatusFromTheExistingSystemEndpoint()
    {
        // Arrange
        var (sut, server) = CreateClient(_ => Json("""{"status":"Healthy","name":"logbook"}"""));

        // Act
        var result = await sut.GetDatabaseStatusAsync(CancellationToken.None);

        // Assert
        result.Value!.Status.Should().Be("Healthy");
        result.Value.Name.Should().Be("logbook");
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/system/database"));
    }

    [Fact]
    public async Task ItShouldReadApiBuildMetadataFromTheExistingSystemEndpoint()
    {
        // Arrange
        var (sut, server) = CreateClient(_ => Json("""{"version":"2.4.0","sha":"abc1234","environment":"Development","builtOn":null}"""));

        // Act
        var result = await sut.GetBuildMetadataAsync(CancellationToken.None);

        // Assert
        result.Value!.Version.Should().Be("2.4.0");
        result.Value.Sha.Should().Be("abc1234");
        result.Value.Environment.Should().Be("Development");
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/system/build"));
    }

    [Fact]
    public async Task ItShouldSurfaceADatabaseOutageWithoutInventingHealth()
    {
        // Arrange
        var (sut, _) = CreateClient(_ => Json("""{"status":"Unhealthy","name":null}""", System.Net.HttpStatusCode.ServiceUnavailable));

        // Act
        var result = await sut.GetDatabaseStatusAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Outcome.Should().Be(ApiOutcomeEnum.ServerError);
        result.Value.Should().BeNull();
    }
}
