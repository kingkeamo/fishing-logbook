using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.ProfileEndpointsTests;

public class WhenTestingFindAnglers : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingFindAnglers(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedLookup()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/profiles/lookup?q=John");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.ProfileRepository.DidNotReceive().FindAnglersAsync(
            Arg.Any<FindAnglersArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAMissingQuery()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "lookup-missing-query"));

        // Act
        var response = await client.GetAsync("/api/profiles/lookup");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.ProfileRepository.DidNotReceive().FindAnglersAsync(
            Arg.Any<FindAnglersArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAQueryShorterThanTheMinimum()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "lookup-short-query"));

        // Act
        var response = await client.GetAsync("/api/profiles/lookup?q=Jo");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.ProfileRepository.DidNotReceive().FindAnglersAsync(
            Arg.Any<FindAnglersArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAQueryLongerThanTheMaximum()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "lookup-long-query"));

        // Act
        var response = await client.GetAsync(
            $"/api/profiles/lookup?q={new string('a', AnglerLookupConstants.MaxQueryLength + 1)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.ProfileRepository.DidNotReceive().FindAnglersAsync(
            Arg.Any<FindAnglersArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExcludeTheSignedInAnglerAndCapTheResults()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "lookup-bounds"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");

        // Act
        var response = await client.GetAsync("/api/profiles/lookup?q=John%20Connolly");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ProfileRepository.Received(1).FindAnglersAsync(
            Arg.Is<FindAnglersArgs>(args =>
                args.RequestingUserId == current!.UserId
                && args.Query == "John Connolly"
                && args.MaxResults == AnglerLookupConstants.MaxResults),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheSafeProfileFieldsWithoutAnEmail()
    {
        // Arrange
        Reset();
        var matchedUserId = Guid.NewGuid();
        _factory.ProfileRepository
            .FindAnglersAsync(Arg.Any<FindAnglersArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<AnglerSummary>>(
            [
                new AnglerSummary
                {
                    UserId = matchedUserId,
                    DisplayName = "John Connolly",
                    HomeRegion = "Galway"
                }
            ]));
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "lookup-results"));

        // Act
        var response = await client.GetAsync("/api/profiles/lookup?q=John");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("@");
        var anglers = await response.Content.ReadFromJsonAsync<IReadOnlyList<AnglerSummaryDto>>();
        anglers.Should().ContainSingle();
        anglers![0].UserId.Should().Be(matchedUserId);
        anglers[0].DisplayName.Should().Be("John Connolly");
        anglers[0].HomeRegion.Should().Be("Galway");
    }

    private void Reset()
    {
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .FindAnglersAsync(Arg.Any<FindAnglersArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<AnglerSummary>>([]));
    }
}
