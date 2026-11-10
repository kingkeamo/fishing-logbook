using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.UserEndpointsTests;

public class WhenTestingOfflineAccessPreference : IClassFixture<SystemApiFactory>
{
    private const string Route = "/api/users/current/offline-access-preference";
    private readonly SystemApiFactory _factory;

    public WhenTestingOfflineAccessPreference(SystemApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid UserId)> SignInAsync()
    {
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: Guid.NewGuid().ToString("N")));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        _factory.OfflineAccessPreferenceRepository.ClearReceivedCalls();
        return (client, current!.UserId);
    }

    [Fact]
    public async Task ItShouldRejectReadingThePreferenceWithoutAuthorization()
    {
        // Arrange
        _factory.OfflineAccessPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(Route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.OfflineAccessPreferenceRepository.DidNotReceive().GetAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectUpdatingThePreferenceWithoutAuthorization()
    {
        // Arrange
        _factory.OfflineAccessPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PutAsJsonAsync(Route, new OfflineAccessPreferenceDto(true));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.OfflineAccessPreferenceRepository.DidNotReceive().SetAsync(
            Arg.Any<Guid>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenThePreferenceCannotBeRead()
    {
        // Arrange
        var (client, userId) = await SignInAsync();
        _factory.OfflineAccessPreferenceRepository
            .GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<OfflineAccessPreferenceDto>("The preference could not be read."));

        // Act
        var response = await client.GetAsync(Route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.OfflineAccessPreferenceRepository.Received(1).GetAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenThePreferenceCannotBeSaved()
    {
        // Arrange
        var (client, userId) = await SignInAsync();
        _factory.OfflineAccessPreferenceRepository
            .SetAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<OfflineAccessPreferenceDto>("The preference could not be saved."));

        // Act
        var response = await client.PutAsJsonAsync(Route, new OfflineAccessPreferenceDto(true));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.OfflineAccessPreferenceRepository.Received(1).SetAsync(
            userId,
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheStoredPreferenceForTheSignedInUser()
    {
        // Arrange
        var (client, userId) = await SignInAsync();
        var enabledAt = DateTimeOffset.Parse("2026-08-17T08:00:00Z");
        _factory.OfflineAccessPreferenceRepository
            .GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new OfflineAccessPreferenceDto(true, enabledAt)));

        // Act
        var response = await client.GetAsync(Route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OfflineAccessPreferenceDto>();
        body.Should().Be(new OfflineAccessPreferenceDto(true, enabledAt));
        await _factory.OfflineAccessPreferenceRepository.Received(1).GetAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveAndReturnTheChosenPreferenceForTheSignedInUser()
    {
        // Arrange
        var (client, userId) = await SignInAsync();
        _factory.OfflineAccessPreferenceRepository
            .SetAsync(userId, false, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new OfflineAccessPreferenceDto(false)));

        // Act
        var response = await client.PutAsJsonAsync(Route, new OfflineAccessPreferenceDto(false));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OfflineAccessPreferenceDto>();
        body!.Enabled.Should().BeFalse();
        await _factory.OfflineAccessPreferenceRepository.Received(1).SetAsync(
            userId,
            false,
            Arg.Any<CancellationToken>());
    }
}
