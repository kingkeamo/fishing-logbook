using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Api.Tests.DiagnosticEndpointsTests;

public class WhenTestingUpload : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingUpload(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldAcceptAValidBatch()
    {
        // Arrange
        var client = _factory.CreateClient();
        var batch = new ClientDiagnosticBatchDto
        {
            Events =
            [
                new ClientDiagnosticEventDto
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Level = "Warning",
                    EventName = DiagnosticEventNames.OfflineDbWriteTimedOut,
                    Message = "write timed out",
                    CorrelationId = Guid.NewGuid(),
                    AnonymousSessionId = Guid.NewGuid(),
                    Metadata = new Dictionary<string, string> { ["elapsedMilliseconds"] = "5000" }
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/diagnostics/client", batch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ItShouldRejectAnOversizedBatch()
    {
        // Arrange
        var client = _factory.CreateClient();
        var batch = new ClientDiagnosticBatchDto
        {
            Events = Enumerable.Range(0, 51)
                .Select(_ => new ClientDiagnosticEventDto
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Level = "Warning",
                    EventName = "TooMany",
                    Message = "x"
                })
                .ToArray()
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/diagnostics/client", batch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ItShouldEchoTheCorrelationId()
    {
        // Arrange
        var client = _factory.CreateClient();
        var correlationId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        client.DefaultRequestHeaders.Add(CorrelationHeaders.CorrelationId, correlationId.ToString("D"));

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.Headers.GetValues(CorrelationHeaders.CorrelationId).Should().Equal(correlationId.ToString("D"));
    }
}
