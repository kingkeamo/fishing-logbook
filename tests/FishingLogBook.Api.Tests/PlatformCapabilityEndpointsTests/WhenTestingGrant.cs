using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Users;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.PlatformCapabilityEndpointsTests;

public class WhenTestingGrant : IClassFixture<PlatformCapabilityApiFactory>
{
    private readonly PlatformCapabilityApiFactory _factory;

    public WhenTestingGrant(PlatformCapabilityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        ResetCapabilityRepository();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            TestGrantPlatformCapabilityStartupFilter.Path,
            GrantBody(Guid.NewGuid(), PlatformCapabilityEnum.Guide));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.UserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
        await _factory.UserPlatformCapabilityRepository.DidNotReceive().HasAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnForbiddenWhenTheUserLacksAdministrator()
    {
        // Arrange
        ResetCapabilityRepository();
        var targetUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");

        // Act
        var response = await client.PostAsJsonAsync(
            TestGrantPlatformCapabilityStartupFilter.Path,
            GrantBody(targetUserId, PlatformCapabilityEnum.Guide));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        current.Should().NotBeNull();
        await _factory.UserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(args =>
                args.UserId == current!.UserId
                && args.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await _factory.UserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreClientSpoofedAdministratorClaims()
    {
        // Arrange
        ResetCapabilityRepository();
        var spoofedUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var targetUserId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");

        // Act
        var response = await client.PostAsJsonAsync(
            TestGrantPlatformCapabilityStartupFilter.Path,
            new
            {
                targetUserId,
                capability = PlatformCapabilityEnum.Guide,
                userId = spoofedUserId,
                administrator = true,
                capabilities = new[] { "Administrator" }
            });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        current.Should().NotBeNull();
        current!.UserId.Should().NotBe(spoofedUserId);
        await _factory.UserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(args =>
                args.UserId == current.UserId
                && args.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await _factory.UserPlatformCapabilityRepository.DidNotReceive().HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(args => args.UserId == spoofedUserId),
            Arg.Any<CancellationToken>());
        await _factory.UserPlatformCapabilityRepository.DidNotReceive().GrantAsync(
            Arg.Any<UserPlatformCapability>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldGrantWhenTheUserHasAdministrator()
    {
        // Arrange
        ResetCapabilityRepository();
        var targetUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        _factory.UserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var args = call.ArgAt<FindUserPlatformCapabilityArgs>(0);
                var isAdministrator = args.UserId == current!.UserId
                    && args.Capability == PlatformCapabilityEnum.Administrator;
                return Result.Ok(isAdministrator);
            });

        // Act
        var response = await client.PostAsJsonAsync(
            TestGrantPlatformCapabilityStartupFilter.Path,
            GrantBody(targetUserId, PlatformCapabilityEnum.Guide));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.UserPlatformCapabilityRepository.Received(1).HasAsync(
            Arg.Is<FindUserPlatformCapabilityArgs>(args =>
                args.UserId == current!.UserId
                && args.Capability == PlatformCapabilityEnum.Administrator),
            Arg.Any<CancellationToken>());
        await _factory.UserPlatformCapabilityRepository.Received(1).GrantAsync(
            Arg.Is<UserPlatformCapability>(association =>
                association.UserId == targetUserId
                && association.Capability == PlatformCapabilityEnum.Guide),
            Arg.Any<CancellationToken>());
    }

    private void ResetCapabilityRepository()
    {
        _factory.UserPlatformCapabilityRepository.ClearReceivedCalls();
        _factory.UserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(false));
        _factory.UserPlatformCapabilityRepository
            .GrantAsync(Arg.Any<UserPlatformCapability>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
    }

    private static object GrantBody(Guid targetUserId, PlatformCapabilityEnum capability)
    {
        return new
        {
            targetUserId,
            capability
        };
    }
}
