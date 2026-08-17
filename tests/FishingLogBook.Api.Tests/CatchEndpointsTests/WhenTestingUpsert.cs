using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.CatchEndpointsTests;

public class WhenTestingUpsert : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingUpsert(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        ResetCatchRepository();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", ValidDto());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACatchWithoutPhotographs()
    {
        // Arrange
        ResetCatchRepository();
        var client = _factory.CreateAuthenticatedClient();
        var dto = new CatchDto(Guid.NewGuid(), DateTimeOffset.UtcNow, []);

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAClientSuppliedOwnerUserId()
    {
        // Arrange
        var clientOwner = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var dto = new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographDto(photographId, catchId, PhotographContentTypeConstants.Jpeg)])
        {
            UserId = clientOwner
        };
        ResetCatchRepository();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", dto);
        var body = await response.Content.ReadFromJsonAsync<CatchDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.UserId.Should().Be(current!.UserId);
        body.UserId.Should().NotBe(clientOwner);
        body.Id.Should().Be(catchId);
        body.Photographs.Should().ContainSingle(photograph => photograph.Id == photographId);
        await _factory.CatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(item =>
                item.UserId == current.UserId
                && item.Id == catchId
                && item.Photographs[0].Id == photographId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectWhenAnotherUserAlreadyOwnsTheCatchId()
    {
        // Arrange
        var dto = ValidDto();
        ResetCatchRepository();
        _factory.CatchRepository
            .UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Catch>(new FishingLogBook.Application.Catches.Errors.CatchOwnershipConflictError()));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(item => item.Id == dto.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenPersistenceFails()
    {
        // Arrange
        var dto = ValidDto();
        ResetCatchRepository();
        _factory.CatchRepository
            .UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Catch>("Failed to save the catch."));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.CatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(item => item.Id == dto.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheSavedCatch()
    {
        // Arrange
        var dto = ValidDto();
        ResetCatchRepository();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", dto);
        var body = await response.Content.ReadFromJsonAsync<CatchDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Id.Should().Be(dto.Id);
        body.CaughtOn.Should().Be(dto.CaughtOn);
        body.UserId.Should().Be(current!.UserId);
        body.Photographs.Should().ContainSingle();
        body.Location.Should().BeNull();
        await _factory.CatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(item => item.Id == dto.Id && item.Photographs.Count == 1 && item.Location == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnInvalidLocation()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var dto = new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographDto(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)],
            new CatchLocationDto(
                91,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion));
        ResetCatchRepository();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistOwnerLocationAsPrivateDeviceGps()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var capturedOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z");
        var location = new CatchLocationDto(
            53.2707,
            -9.0568,
            12,
            capturedOn,
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        var dto = new CatchDto(
            catchId,
            capturedOn,
            [new CatchPhotographDto(photographId, catchId, PhotographContentTypeConstants.Jpeg)],
            location);
        ResetCatchRepository();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");

        // Act
        var response = await client.PostAsJsonAsync("/api/catches", dto);
        var body = await response.Content.ReadFromJsonAsync<CatchDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.UserId.Should().Be(current!.UserId);
        body.Location.Should().Be(location);
        body.Location!.Visibility.Should().Be(LocationDefaults.Private);
        body.Location.Source.Should().Be(LocationDefaults.DeviceGps);
        body.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
        await _factory.CatchRepository.Received(1).UpsertAsync(
            Arg.Is<Catch>(item =>
                item.Id == catchId
                && item.UserId == current.UserId
                && item.Location != null
                && item.Location.Latitude == 53.2707
                && item.Location.Longitude == -9.0568
                && item.Location.AccuracyMetres == 12
                && item.Location.Source == LocationDefaults.DeviceGps
                && item.Location.Visibility == LocationDefaults.Private
                && item.Location.ConsentVersion == LocationDefaults.ConsentVersion),
            Arg.Any<CancellationToken>());
    }

    private void ResetCatchRepository()
    {
        _factory.CatchRepository.ClearReceivedCalls();
        _factory.CatchRepository
            .UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));
    }

    private static CatchDto ValidDto()
    {
        var catchId = Guid.NewGuid();
        return new CatchDto(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographDto(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)]);
    }
}
