using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.CatchEndpointsTests;

public class WhenTestingPhotograph : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingPhotograph(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedUploadRequest()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();
        var catchId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs/upload-url",
            new PhotographUploadRequestDto(Guid.NewGuid(), "image/jpeg"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CatchRepository.DidNotReceive().GetPhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedRecordRequest()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();
        var catchId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs",
            new RecordPhotographDto(Guid.NewGuid(), "object-key", "image/jpeg"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CatchRepository.DidNotReceive().GetPhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAnUnsupportedUploadContentType()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var catchId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs/upload-url",
            new PhotographUploadRequestDto(Guid.NewGuid(), "image/gif"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.DidNotReceive().GetPhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenPhotographLookupFails()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        GivenCatchOwnedByCurrentUser(catchId, current!.UserId);
        _factory.CatchRepository.GetPhotographAsync(
                Arg.Any<GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail<CatchPhotograph?>("database unavailable"));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs/upload-url",
            new PhotographUploadRequestDto(photographId, "image/jpeg"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.CatchRepository.Received(1).GetPhotographAsync(
            Arg.Is<GetCatchPhotographArgs>(query =>
                query.CatchId == catchId
                && query.PhotographId == photographId),
            Arg.Any<CancellationToken>());
        await _factory.ObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenObjectStorageIsNotConfigured()
    {
        // Arrange
        Reset();
        _factory.ObjectStorage.IsConfigured.Returns(false);
        var client = _factory.CreateAuthenticatedClient();
        var catchId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs/upload-url",
            new PhotographUploadRequestDto(Guid.NewGuid(), "image/jpeg"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.CatchRepository.DidNotReceive().GetPhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeriveAStableCatchScopedObjectKey()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        GivenCatchOwnedByCurrentUser(catchId, current!.UserId);
        _factory.CatchRepository.GetPhotographAsync(
                Arg.Any<GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchPhotograph?>(
                new CatchPhotograph
                {
                    Id = photographId,
                    CatchId = catchId,
                    ContentType = "image/jpeg"
                }));
        var request = new PhotographUploadRequestDto(photographId, "image/jpeg");

        // Act
        var firstResponse = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs/upload-url",
            request);
        var secondResponse = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs/upload-url",
            request);
        var first = await firstResponse.Content.ReadFromJsonAsync<PhotographUploadDto>();
        var second = await secondResponse.Content.ReadFromJsonAsync<PhotographUploadDto>();

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        first!.ObjectKey.Should().Be(
            $"catch-photographs/{catchId:D}/{photographId:D}");
        second!.ObjectKey.Should().Be(first.ObjectKey);
        await _factory.CatchRepository.Received(2).GetPhotographAsync(
            Arg.Is<GetCatchPhotographArgs>(query =>
                query.CaughtByUserId == current.UserId
                && query.CatchId == catchId
                && query.PhotographId == photographId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNotFoundForAPhotographOutsideTheCurrentOwner()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        _factory.CatchRepository.GetPhotographAsync(
                Arg.Any<GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchPhotograph?>(null));
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs/upload-url",
            new PhotographUploadRequestDto(photographId, "image/jpeg"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.ObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptRepeatedConfirmationWithoutCreatingAnotherRecord()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        GivenCatchOwnedByCurrentUser(catchId, current!.UserId);
        _factory.CatchRepository.GetPhotographAsync(
                Arg.Any<GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchPhotograph?>(
                new CatchPhotograph
                {
                    Id = photographId,
                    CatchId = catchId,
                    ContentType = "image/jpeg"
                }));
        var request = new RecordPhotographDto(
            photographId,
            $"catch-photographs/{catchId:D}/{photographId:D}",
            "image/jpeg");

        // Act
        var first = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs",
            request);
        var second = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs",
            request);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.CatchRepository.Received(2).GetPhotographAsync(
            Arg.Is<GetCatchPhotographArgs>(query =>
                query.CaughtByUserId == current.UserId
                && query.CatchId == catchId
                && query.PhotographId == photographId),
            Arg.Any<CancellationToken>());
        await _factory.CatchRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Catch>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAReplacementObjectKey()
    {
        // Arrange
        Reset();
        var client = _factory.CreateAuthenticatedClient();
        var current = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        GivenCatchOwnedByCurrentUser(catchId, current!.UserId);
        _factory.CatchRepository.GetPhotographAsync(
                Arg.Any<GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchPhotograph?>(
                new CatchPhotograph
                {
                    Id = photographId,
                    CatchId = catchId,
                    ContentType = "image/jpeg"
                }));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/catches/{catchId:D}/photographs",
            new RecordPhotographDto(
                photographId,
                "catch-photographs/another-catch/replacement",
                "image/jpeg"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CatchRepository.Received(1).GetPhotographAsync(
            Arg.Is<GetCatchPhotographArgs>(query =>
                query.CatchId == catchId
                && query.PhotographId == photographId),
            Arg.Any<CancellationToken>());
    }

    private void GivenCatchOwnedByCurrentUser(Guid catchId, Guid currentUserId)
    {
        _factory.CatchRepository.GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(new Catch
            {
                Id = catchId,
                CaughtByUserId = currentUserId,
                RecordedByUserId = currentUserId
            }));
    }

    private void Reset()
    {
        _factory.CatchRepository.ClearReceivedCalls();
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ObjectStorage.IsConfigured.Returns(true);
        _factory.ObjectStorage.CreateUploadUrlAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/upload"));
    }
}
