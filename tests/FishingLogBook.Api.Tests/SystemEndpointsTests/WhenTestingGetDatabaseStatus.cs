using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Domain.SystemStatus;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using NSubstitute;

namespace FishingLogBook.Api.Tests.SystemEndpointsTests;

public class WhenTestingGetDatabaseStatus : BaseSystemEndpointsTest
{
    public WhenTestingGetDatabaseStatus(SystemApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWithDegradedWhenNoRecordExists()
    {
        // Arrange
        Factory.SystemRepository.ClearReceivedCalls();
        Factory.SystemRepository
            .GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns((SystemHealth?)null);
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/system/database");
        var body = await response.Content.ReadFromJsonAsync<DatabaseTestDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body!.Status.Should().Be("Degraded");
        await Factory.SystemRepository.Received(1).GetSystemHealthAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWithUnhealthyWhenRepositoryThrows()
    {
        // Arrange
        Factory.SystemRepository.ClearReceivedCalls();
        Factory.SystemRepository
            .GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns<SystemHealth?>(_ => throw new InvalidOperationException("database unavailable"));
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/system/database");
        var body = await response.Content.ReadFromJsonAsync<DatabaseTestDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body!.Status.Should().Be("Unhealthy");
        await Factory.SystemRepository.Received(1).GetSystemHealthAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnHealthyWhenRecordExists()
    {
        // Arrange
        Factory.SystemRepository.ClearReceivedCalls();
        Factory.SystemRepository
            .GetSystemHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new SystemHealthBuilder().WithName("FishingLogBook database online").Build());
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/system/database");
        var body = await response.Content.ReadFromJsonAsync<DatabaseTestDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Status.Should().Be("Healthy");
        body.Name.Should().Be("FishingLogBook database online");
        await Factory.SystemRepository.Received(1).GetSystemHealthAsync(Arg.Any<CancellationToken>());
    }
}
