using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Domain.Users;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.TripEndpointsTests;

public class WhenTestingPhotographs : IClassFixture<SystemApiFactory>
{
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");
    private static readonly DateTimeOffset AddedOn = DateTimeOffset.Parse("2026-08-17T09:00:00Z");

    private readonly SystemApiFactory _factory;

    public WhenTestingPhotographs(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedUploadRequest()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{Guid.NewGuid():D}/photographs/upload-url",
            new PhotographUploadRequestDto(Guid.NewGuid(), PhotographContentTypeConstants.Jpeg));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TripPhotographRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripPhotograph>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnsupportedContentType()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{Guid.NewGuid():D}/photographs/upload-url",
            new PhotographUploadRequestDto(Guid.NewGuid(), "application/pdf"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ItShouldNotFindAnUploadForAnotherAnglersTrip()
    {
        // Arrange
        Reset();
        var tripId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, Guid.NewGuid())));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/photographs/upload-url",
            new PhotographUploadRequestDto(Guid.NewGuid(), PhotographContentTypeConstants.Jpeg));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(tripId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldNotRevealWhetherAnotherAnglersTripExists()
    {
        // Arrange
        Reset();
        var knownTripId = Guid.NewGuid();
        var unknownTripId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(knownTripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(knownTripId, Guid.NewGuid())));
        _factory.TripRepository.GetByIdAsync(unknownTripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var known = await client.PostAsJsonAsync(
            $"/api/trips/{knownTripId:D}/photographs/upload-url",
            new PhotographUploadRequestDto(Guid.NewGuid(), PhotographContentTypeConstants.Jpeg));
        var unknown = await client.PostAsJsonAsync(
            $"/api/trips/{unknownTripId:D}/photographs/upload-url",
            new PhotographUploadRequestDto(Guid.NewGuid(), PhotographContentTypeConstants.Jpeg));

        // Assert
        known.StatusCode.Should().Be(unknown.StatusCode);
        (await known.Content.ReadAsStringAsync())
            .Should().Be(await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ItShouldRejectARecordWhoseObjectKeyIsNotTripScoped()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/photographs",
            new RecordTripPhotographDto(
                photographId,
                $"catches/{current.UserId:D}/{tripId:D}/{photographId:D}",
                PhotographContentTypeConstants.Jpeg,
                AddedOn));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TripPhotographRepository.DidNotReceive().UpsertAsync(
            Arg.Any<TripPhotograph>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordAPhotographForTheAnglersOwnTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var objectKey = $"trips/{current!.UserId:D}/{tripId:D}/{photographId:D}";
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current.UserId, TripStatusEnum.Completed)));
        var capturedOn = StartedOn.AddMinutes(30);

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/trips/{tripId:D}/photographs",
            new RecordTripPhotographDto(
                photographId,
                objectKey,
                PhotographContentTypeConstants.Jpeg,
                AddedOn,
                capturedOn));
        var body = await response.Content.ReadFromJsonAsync<TripPhotographDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Id.Should().Be(photographId);
        body.TripId.Should().Be(tripId);
        body.ObjectKey.Should().Be(objectKey);
        body.CapturedOn.Should().Be(capturedOn);
        body.AddedOn.Should().Be(AddedOn);
        await _factory.TripPhotographRepository.Received(1).UpsertAsync(
            Arg.Is<TripPhotograph>(photograph =>
                photograph.Id == photographId
                && photograph.TripId == tripId
                && photograph.ObjectKey == objectKey
                && photograph.CapturedOn == capturedOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeleteAPhotographFromTheAnglersOwnTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var objectKey = $"trips/{current!.UserId:D}/{tripId:D}/{photographId:D}";
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current.UserId)));
        _factory.TripPhotographRepository.GetByIdAsync(photographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(new TripPhotograph
            {
                Id = photographId,
                TripId = tripId,
                ContributedByUserId = current!.UserId,
                ObjectKey = objectKey,
                ContentType = PhotographContentTypeConstants.Jpeg,
                AddedOn = AddedOn
            }));

        // Act
        var response = await client.DeleteAsync(
            $"/api/trips/{tripId:D}/photographs/{photographId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.TripPhotographRepository.Received(1).DeleteAsync(
            photographId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotDeleteAPhotographBelongingToAnotherTrip()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var tripId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        _factory.TripRepository.GetByIdAsync(tripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(Trip(tripId, current!.UserId)));
        _factory.TripPhotographRepository.GetByIdAsync(photographId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(new TripPhotograph
            {
                Id = photographId,
                TripId = Guid.NewGuid(),
                ObjectKey = "trips/other/other/other",
                ContentType = PhotographContentTypeConstants.Jpeg,
                AddedOn = AddedOn
            }));

        // Act
        var response = await client.DeleteAsync(
            $"/api/trips/{tripId:D}/photographs/{photographId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.TripPhotographRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    private static Trip Trip(
        Guid tripId,
        Guid ownerUserId,
        TripStatusEnum status = TripStatusEnum.Active)
    {
        return new Trip
        {
            Id = tripId,
            OwnerUserId = ownerUserId,
            Status = status,
            StartedOn = StartedOn,
            EndedOn = status == TripStatusEnum.Completed ? StartedOn.AddHours(3) : null
        };
    }

    private void Reset()
    {
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ObjectStorage.IsConfigured.Returns(true);
        _factory.ObjectStorage.CreateUploadUrlAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/upload"));
        _factory.TripRepository.ClearReceivedCalls();
        _factory.TripPhotographRepository.ClearReceivedCalls();
        _factory.TripPhotographRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripPhotograph?>(null));
        _factory.TripPhotographRepository
            .UpsertAsync(Arg.Any<TripPhotograph>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<TripPhotograph>(0)));
        _factory.TripPhotographRepository
            .DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
    }
}
