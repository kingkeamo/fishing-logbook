using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Api.Tests.SystemEndpointsTests;

public class WhenTestingGetBuildMetadata : BaseSystemEndpointsTest
{
    public WhenTestingGetBuildMetadata(SystemApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ItShouldReturnOnlyNonSensitiveBuildMetadata()
    {
        var response = await Factory.CreateClient().GetAsync("/api/system/build");
        var body = await response.Content.ReadFromJsonAsync<BuildMetadataDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be(new BuildMetadataDto(
            "0.1.0",
            "0123456789abcdef0123456789abcdef01234567",
            "prod",
            DateTimeOffset.Parse("2026-08-22T00:00:00Z")));
    }
}
