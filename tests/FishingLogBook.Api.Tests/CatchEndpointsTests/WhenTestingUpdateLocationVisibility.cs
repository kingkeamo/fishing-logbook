using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.CatchEndpointsTests;

public class WhenTestingUpdateLocationVisibility : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingUpdateLocationVisibility(SystemApiFactory factory)
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
        var response = await client.PatchAsJsonAsync(
            $"/api/catches/{Guid.NewGuid():D}/location-visibility",
            new UpdateCatchLocationVisibilityDto(LocationDefaults.Public));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CatchRepository.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<PersistCatchLocationVisibilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNotFoundWhenTheCatchIsMissing()
    {
        // Arrange
        ResetRepositories();
        var catchId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "missing-visibility"));

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/catches/{catchId:D}/location-visibility",
            new UpdateCatchLocationVisibilityDto(LocationDefaults.Public));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.CatchRepository.Received(1).GetByIdAsync(catchId, Arg.Any<CancellationToken>());
        await _factory.CatchRepository.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<PersistCatchLocationVisibilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectUnknownVisibility()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "invalid-visibility"));

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/catches/{Guid.NewGuid():D}/location-visibility",
            new UpdateCatchLocationVisibilityDto("FriendsOnly"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _factory.CatchRepository.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<PersistCatchLocationVisibilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDenyWhenTheCallerDoesNotOwnTheCatch()
    {
        // Arrange
        ResetRepositories();
        var ownerClient = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "visibility-owner"));
        var owner = await ownerClient.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var catchRecord = LocatedCatch(owner!.UserId);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        var viewer = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "visibility-viewer"));

        // Act
        var response = await viewer.PatchAsJsonAsync(
            $"/api/catches/{catchRecord.Id:D}/location-visibility",
            new
            {
                visibility = LocationDefaults.Public,
                userId = owner.UserId,
                actorUserId = owner.UserId,
                administrator = true
            });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _factory.CatchRepository.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<PersistCatchLocationVisibilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectWhenTheCatchHasNoLocation()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "no-location-owner"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var catchRecord = new Catch
        {
            Id = Guid.NewGuid(),
            CaughtByUserId = current!.UserId,
            RecordedByUserId = current.UserId,
            CaughtOn = DateTimeOffset.UtcNow
        };
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/catches/{catchRecord.Id:D}/location-visibility",
            new UpdateCatchLocationVisibilityDto(LocationDefaults.Approximate));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<PersistCatchLocationVisibilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUpdateVisibilityForTheOwner()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "visibility-update-owner"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var catchRecord = LocatedCatch(current!.UserId);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        _factory.CatchRepository
            .UpdateLocationVisibilityAsync(Arg.Any<PersistCatchLocationVisibilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/catches/{catchRecord.Id:D}/location-visibility",
            new UpdateCatchLocationVisibilityDto(LocationDefaults.Public));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.CatchRepository.Received(1).UpdateLocationVisibilityAsync(
            Arg.Is<PersistCatchLocationVisibilityArgs>(args =>
                args.CatchId == catchRecord.Id
                && args.CaughtByUserId == current.UserId
                && args.Visibility == LocationDefaults.Public),
            Arg.Any<CancellationToken>());
        await _factory.CatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    private void ResetRepositories()
    {
        _factory.CatchRepository.ClearReceivedCalls();
        _factory.CatchRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(null));
        _factory.CatchRepository
            .UpdateLocationVisibilityAsync(Arg.Any<PersistCatchLocationVisibilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
    }

    private static Catch LocatedCatch(Guid ownerUserId)
    {
        var catchId = Guid.NewGuid();
        return new Catch
        {
            Id = catchId,
            CaughtByUserId = ownerUserId,
            RecordedByUserId = ownerUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Location = CatchLocation.TryCreate(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion)
        };
    }
}
