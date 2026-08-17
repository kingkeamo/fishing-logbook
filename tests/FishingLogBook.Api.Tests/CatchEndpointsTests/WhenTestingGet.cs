using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.CatchEndpointsTests;

public class WhenTestingGet : IClassFixture<SystemApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SystemApiFactory _factory;

    public WhenTestingGet(SystemApiFactory factory)
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
        var response = await client.GetAsync($"/api/catches/{Guid.NewGuid():D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CatchRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNotFoundWhenTheCatchIsMissing()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/catches/{catchId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.CatchRepository.Received(1).GetByIdAsync(catchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnACatchWithoutLocationWhenNoneWasSaved()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "no-location-get"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        var catchRecord = new Catch
        {
            Id = Guid.NewGuid(),
            UserId = current!.UserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z")
        };
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));

        // Act
        var response = await client.GetAsync($"/api/catches/{catchRecord.Id:D}");
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<CatchViewDto>(json, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().NotContain("\"latitude\":");
        json.Should().NotContain("53.2707");
        body.Should().NotBeNull();
        body!.Location.Should().BeNull();
        await _factory.CatchRepository.Received(1).GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnExactCoordinatesToTheOwner()
    {
        // Arrange
        ResetRepositories();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "owner-get"));
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        current.Should().NotBeNull();
        var catchRecord = LocatedCatch(current!.UserId);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));

        // Act
        var response = await client.GetAsync($"/api/catches/{catchRecord.Id:D}");
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<CatchViewDto>(json, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("\"latitude\":");
        json.Should().Contain("53.2707");
        json.Should().Contain("-9.0568");
        body.Should().NotBeNull();
        body!.Location.Should().NotBeNull();
        body.Location!.Mode.Should().Be(LocationDefaults.ExposureExact);
        body.Location.Latitude.Should().Be(53.2707);
        body.Location.Longitude.Should().Be(-9.0568);
        await _factory.CatchRepository.Received(1).GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitExactCoordinatesFromPrivateNonOwnerJson()
    {
        // Arrange
        ResetRepositories();
        var ownerClient = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "private-owner"));
        var owner = await ownerClient.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        owner.Should().NotBeNull();
        var catchRecord = LocatedCatch(owner!.UserId);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        var viewer = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "private-viewer"));

        // Act
        var response = await viewer.GetAsync($"/api/catches/{catchRecord.Id:D}?userId={owner.UserId:D}");
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<CatchViewDto>(json, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().NotContain("53.2707");
        json.Should().NotContain("-9.0568");
        json.Should().NotContain("\"latitude\":");
        json.Should().NotContain("\"longitude\":");
        body.Should().NotBeNull();
        body!.Location.Should().NotBeNull();
        body.Location!.Mode.Should().Be(LocationDefaults.ExposureNone);
        body.Location.Latitude.Should().BeNull();
        await _factory.CatchRepository.Received(1).GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>());
        await _factory.UserPlatformCapabilityRepository.DidNotReceive().HasAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PlatformCapabilityEnum.Guide)]
    [InlineData(PlatformCapabilityEnum.FishingVenueManager)]
    [InlineData(PlatformCapabilityEnum.CompetitionOrganiser)]
    [InlineData(PlatformCapabilityEnum.Administrator)]
    public async Task ItShouldDenyExactCoordinatesWhenTheViewerHasAPrivilegedCapability(
        PlatformCapabilityEnum capability)
    {
        // Arrange
        ResetRepositories();
        var ownerClient = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: $"owner-{capability}"));
        var owner = await ownerClient.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        owner.Should().NotBeNull();
        var catchRecord = LocatedCatch(owner!.UserId);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        var viewerClient = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: $"viewer-{capability}"));
        var viewer = await viewerClient.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        viewer.Should().NotBeNull();
        _factory.UserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var lookup = call.ArgAt<FindUserPlatformCapabilityArgs>(0);
                return Result.Ok(lookup.UserId == viewer!.UserId && lookup.Capability == capability);
            });

        // Act
        var response = await viewerClient.GetAsync($"/api/catches/{catchRecord.Id:D}");
        var json = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().NotContain("53.2707");
        json.Should().NotContain("-9.0568");
        json.Should().NotContain("\"latitude\":");
        await _factory.CatchRepository.Received(1).GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>());
        await _factory.UserPlatformCapabilityRepository.DidNotReceive().HasAsync(
            Arg.Any<FindUserPlatformCapabilityArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnApproximateFieldsWithoutOriginalCoordinates()
    {
        // Arrange
        ResetRepositories();
        var ownerClient = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "approx-owner"));
        var owner = await ownerClient.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        owner.Should().NotBeNull();
        var catchRecord = LocatedCatch(owner!.UserId, LocationDefaults.Approximate);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        var viewer = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "approx-viewer"));

        // Act
        var response = await viewer.GetAsync($"/api/catches/{catchRecord.Id:D}");
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<CatchViewDto>(json, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().NotContain("53.2707");
        json.Should().NotContain("-9.0568");
        json.Should().NotContain("\"latitude\":");
        json.Should().NotContain("\"longitude\":");
        json.Should().Contain("\"approximateLatitude\":");
        body.Should().NotBeNull();
        body!.Location!.Mode.Should().Be(LocationDefaults.ExposureApproximate);
        body.Location.ApproximateLatitude.Should().Be(53.275);
        body.Location.ApproximateLongitude.Should().Be(-9.075);
        body.Location.Latitude.Should().BeNull();
        await _factory.CatchRepository.Received(1).GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitGpsWhenFishingVenueOnly()
    {
        // Arrange
        ResetRepositories();
        var ownerClient = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "venue-owner"));
        var owner = await ownerClient.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        owner.Should().NotBeNull();
        var catchRecord = LocatedCatch(owner!.UserId, LocationDefaults.FishingVenueOnly);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        var viewer = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "venue-viewer"));

        // Act
        var response = await viewer.GetAsync($"/api/catches/{catchRecord.Id:D}");
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<CatchViewDto>(json, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().NotContain("53.2707");
        json.Should().NotContain("\"latitude\":");
        json.Should().NotContain("\"approximateLatitude\":");
        body!.Location!.Mode.Should().Be(LocationDefaults.ExposureFishingVenue);
        body.Location.Latitude.Should().BeNull();
        body.Location.FishingVenueId.Should().BeNull();
        await _factory.CatchRepository.Received(1).GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnExactCoordinatesWhenPublic()
    {
        // Arrange
        ResetRepositories();
        var ownerClient = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "public-owner"));
        var owner = await ownerClient.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        owner.Should().NotBeNull();
        var catchRecord = LocatedCatch(owner!.UserId, LocationDefaults.Public);
        _factory.CatchRepository
            .GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(catchRecord));
        var viewer = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "public-viewer"));

        // Act
        var response = await viewer.GetAsync($"/api/catches/{catchRecord.Id:D}");
        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<CatchViewDto>(json, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("\"latitude\":");
        json.Should().Contain("53.2707");
        body.Should().NotBeNull();
        body!.Location!.Mode.Should().Be(LocationDefaults.ExposureExact);
        body.Location.Latitude.Should().Be(53.2707);
        body.Location.Longitude.Should().Be(-9.0568);
        await _factory.CatchRepository.Received(1).GetByIdAsync(catchRecord.Id, Arg.Any<CancellationToken>());
    }

    private void ResetRepositories()
    {
        _factory.CatchRepository.ClearReceivedCalls();
        _factory.UserPlatformCapabilityRepository.ClearReceivedCalls();
        _factory.CatchRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(null));
        _factory.UserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(false));
    }

    private static Catch LocatedCatch(Guid ownerUserId, string visibility = LocationDefaults.Private)
    {
        var catchId = Guid.NewGuid();
        return new Catch
        {
            Id = catchId,
            UserId = ownerUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Location = CatchLocation.TryCreate(
                53.2707,
                -9.0568,
                5,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                LocationDefaults.DeviceGps,
                visibility,
                LocationDefaults.ConsentVersion)
        };
    }
}
