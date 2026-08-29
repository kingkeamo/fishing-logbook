using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.CatchEndpointsTests;

public class WhenTestingList : IClassFixture<SystemApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SystemApiFactory _factory;

    public WhenTestingList(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CatchRepository.DidNotReceive().GetActivityForUserAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturn503WhenTheRepositoryFails()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "list-failure"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        _factory.CatchRepository
            .GetActivityForUserAsync(current!.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<CatchDetail>>("Failed to save the catch."));

        // Act
        var response = await client.GetAsync("/api/catches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.CatchRepository.Received(1).GetActivityForUserAsync(
            current.UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheUserHasNoCatches()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "list-empty"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();

        // Act
        var response = await client.GetAsync("/api/catches");
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<CatchViewDto>>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body.Should().BeEmpty();
        await _factory.CatchRepository.Received(1).GetActivityForUserAsync(
            current!.UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnOnlyTheCurrentUsersCatches()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "list-owner"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        var catchRecord = new Catch
        {
            Id = Guid.NewGuid(),
            UserId = current!.UserId,
            AnglerUserId = current.UserId,
            RecordedByUserId = current.UserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            SpeciesName = "Pike"
        };
        _factory.CatchRepository
            .GetActivityForUserAsync(current.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<CatchDetail>>([new CatchDetail { Catch = catchRecord }]));

        // Act
        var response = await client.GetAsync("/api/catches");
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<CatchViewDto>>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().ContainSingle(view => view.Id == catchRecord.Id && view.SpeciesName == "Pike");
        await _factory.CatchRepository.Received(1).GetActivityForUserAsync(
            current.UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIncludeAndNameACatchRecordedForAnotherAngler()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "list-recorder"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        var anglerUserId = Guid.NewGuid();
        var recordedForAnother = new Catch
        {
            Id = Guid.NewGuid(),
            UserId = anglerUserId,
            AnglerUserId = anglerUserId,
            RecordedByUserId = current!.UserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            SpeciesName = "Brown Trout"
        };
        _factory.CatchRepository
            .GetActivityForUserAsync(current.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<CatchDetail>>(
            [
                new CatchDetail
                {
                    Catch = recordedForAnother,
                    AnglerName = "Patrick Connolly",
                    RecordedByName = "Current User"
                }
            ]));

        // Act
        var response = await client.GetAsync("/api/catches");
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<CatchViewDto>>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().ContainSingle(view =>
            view.Id == recordedForAnother.Id
            && view.UserId == anglerUserId
            && view.AnglerName == "Patrick Connolly"
            && view.RecordedByUserId == current.UserId
            && view.RecordedByName == "Current User");
    }

    private void ResetRepositories()
    {
        _factory.CatchRepository.ClearReceivedCalls();
        _factory.CatchRepository
            .GetActivityForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<CatchDetail>>([]));
    }
}
