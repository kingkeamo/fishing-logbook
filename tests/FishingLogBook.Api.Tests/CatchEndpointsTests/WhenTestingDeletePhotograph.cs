using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Api.Tests.CatchEndpointsTests;

public class WhenTestingDeletePhotograph : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingDeletePhotograph(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectAnUnauthenticatedRequest()
    {
        // Arrange
        Reset();
        var client = _factory.CreateClient();
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/api/catches/{catchId:D}/photographs/{photographId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CatchRepository.DidNotReceive().GetPhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
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
        var photographId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/api/catches/{catchId:D}/photographs/{photographId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.CatchRepository.DidNotReceive().GetPhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
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
        var response = await client.DeleteAsync($"/api/catches/{catchId:D}/photographs/{photographId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.ObjectStorage.DidNotReceive().DeleteObjectAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnServiceUnavailableWhenObjectStorageDeletionFails()
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
        _factory.ObjectStorage.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("R2 unavailable"));

        // Act
        var response = await client.DeleteAsync($"/api/catches/{catchId:D}/photographs/{photographId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.CatchRepository.DidNotReceive().DeletePhotographAsync(
            Arg.Any<GetCatchPhotographArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDeleteTheOwnedPhotograph()
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
        var response = await client.DeleteAsync($"/api/catches/{catchId:D}/photographs/{photographId:D}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.ObjectStorage.Received(1).DeleteObjectAsync(
            $"catch-photographs/{catchId:D}/{photographId:D}",
            Arg.Any<CancellationToken>());
        await _factory.CatchRepository.Received(1).DeletePhotographAsync(
            Arg.Is<GetCatchPhotographArgs>(query =>
                query.CaughtByUserId == current.UserId
                && query.CatchId == catchId
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
        _factory.CatchRepository.DeletePhotographAsync(
                Arg.Any<GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
    }
}
