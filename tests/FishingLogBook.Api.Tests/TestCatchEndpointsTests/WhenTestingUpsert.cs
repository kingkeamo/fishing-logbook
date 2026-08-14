using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Domain.TestCatches;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TestCatchEndpointsTests;

public class WhenTestingUpsert : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingUpsert(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldReturnTheSameRecord_WhenPostedTwiceWithTheSameId()
    {
        // Arrange
        var record = new TestCatchRecord
        {
            Id = Guid.Parse("9b7e2d14-0c58-4a91-8e26-3f1d0a7c4b85"),
            SpeciesName = "Trout",
            CaughtOn = DateTimeOffset.Parse("2026-08-14T14:00:00Z"),
            Notes = "Weir pool"
        };
        _factory.TestCatchRepository
            .UpsertAsync(Arg.Any<TestCatchRecord>(), Arg.Any<CancellationToken>())
            .Returns(record);
        var client = _factory.CreateClient();
        var dto = new TestCatchDto(record.Id, record.SpeciesName, record.CaughtOn, record.Notes);

        // Act
        var first = await client.PostAsJsonAsync("/api/test-catches", dto);
        var second = await client.PostAsJsonAsync("/api/test-catches", dto);
        var firstBody = await first.Content.ReadFromJsonAsync<TestCatchDto>();
        var secondBody = await second.Content.ReadFromJsonAsync<TestCatchDto>();

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        firstBody.Should().Be(dto);
        secondBody.Should().Be(dto);
        await _factory.TestCatchRepository.Received(2).UpsertAsync(
            Arg.Is<TestCatchRecord>(item => item.Id == record.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectTheCatch_WhenSpeciesIsMissing()
    {
        // Arrange
        _factory.TestCatchRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();
        var dto = new TestCatchDto(Guid.NewGuid(), "  ", DateTimeOffset.UtcNow, null);

        // Act
        var response = await client.PostAsJsonAsync("/api/test-catches", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TestCatchRepository.DidNotReceive()
            .UpsertAsync(Arg.Any<TestCatchRecord>(), Arg.Any<CancellationToken>());
    }
}

public class WhenTestingList : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingList(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldReturnStoredCatches()
    {
        // Arrange
        var record = new TestCatchRecord
        {
            Id = Guid.Parse("1c9a4e70-6d2b-4f18-a5c3-8e0b9d47f012"),
            SpeciesName = "Roach",
            CaughtOn = DateTimeOffset.Parse("2026-08-14T15:00:00Z"),
            Notes = null
        };
        _factory.TestCatchRepository
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchRecord>>([record]));
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test-catches");
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<TestCatchDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().ContainSingle()
            .Which.Should().Be(new TestCatchDto(record.Id, record.SpeciesName, record.CaughtOn, record.Notes));
    }
}
