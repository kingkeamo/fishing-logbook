using System.Net;
using System.Net.Http.Json;
using FishingLogBook.Domain.SystemStatus;
using FishingLogBook.Shared.SystemStatus;
using FluentAssertions;
using NSubstitute;

namespace FishingLogBook.IntegrationTests;

public class SystemEndpointsTests : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public SystemEndpointsTests(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ShouldReturnHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task GetDatabaseStatus_ShouldReturnHealthy_WhenRecordExists()
    {
        // Arrange
        _factory.SystemRepository
            .GetSystemTestRecordAsync(Arg.Any<CancellationToken>())
            .Returns(new SystemTestRecord
            {
                Id = Guid.NewGuid(),
                Name = "FishingLogBook database online",
                CreatedOn = DateTimeOffset.UtcNow
            });
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/system/database");
        var body = await response.Content.ReadFromJsonAsync<DatabaseTestResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Status.Should().Be("Healthy");
        body.Name.Should().Be("FishingLogBook database online");
    }

    [Fact]
    public async Task GetDatabaseStatus_ShouldReturnServiceUnavailable_WhenNoRecordExists()
    {
        // Arrange
        _factory.SystemRepository
            .GetSystemTestRecordAsync(Arg.Any<CancellationToken>())
            .Returns((SystemTestRecord?)null);
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/system/database");
        var body = await response.Content.ReadFromJsonAsync<DatabaseTestResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body!.Status.Should().Be("Degraded");
    }

    [Fact]
    public async Task GetDatabaseStatus_ShouldReturnServiceUnavailable_WhenRepositoryThrows()
    {
        // Arrange
        _factory.SystemRepository
            .GetSystemTestRecordAsync(Arg.Any<CancellationToken>())
            .Returns<SystemTestRecord?>(_ => throw new InvalidOperationException("database unavailable"));
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/system/database");
        var body = await response.Content.ReadFromJsonAsync<DatabaseTestResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body!.Status.Should().Be("Unhealthy");
    }
}
