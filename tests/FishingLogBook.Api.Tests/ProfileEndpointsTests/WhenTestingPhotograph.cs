using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.ProfileEndpointsTests;

public class WhenTestingPhotograph : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingPhotograph(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectUploadUrlWhenObjectStorageIsNotConfigured()
    {
        // Arrange
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ObjectStorage.IsConfigured.Returns(false);
        var client = _factory.CreateAuthenticatedClient();
        var request = new PhotographUploadRequestDto(Guid.NewGuid(), "image/jpeg");

        // Act
        var response = await client.PostAsJsonAsync("/api/profiles/me/photograph/upload-url", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await _factory.ObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.DidNotReceive().GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnUploadUrlWhenObjectStorageIsConfigured()
    {
        // Arrange
        var photographId = Guid.NewGuid();
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ObjectStorage.IsConfigured.Returns(true);
        _factory.ProfileRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        _factory.ObjectStorage
            .CreateUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/upload"));
        var client = _factory.CreateAuthenticatedClient();
        var request = new PhotographUploadRequestDto(photographId, "image/jpeg");

        // Act
        var response = await client.PostAsJsonAsync("/api/profiles/me/photograph/upload-url", request);
        var body = await response.Content.ReadFromJsonAsync<PhotographUploadDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.UploadUrl.Should().Be("https://storage.test/upload");
        body.ObjectKey.Should().StartWith("profiles/");
        body.ObjectKey.Should().EndWith($"/{photographId:D}");
        await _factory.ObjectStorage.Received(1).CreateUploadUrlAsync(
            Arg.Is<string>(key => key.EndsWith($"/{photographId:D}", StringComparison.Ordinal)),
            "image/jpeg",
            TimeSpan.FromMinutes(15),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAMismatchedPhotographObjectKey()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        var request = new RecordPhotographDto(Guid.NewGuid(), "profiles/other/photo", "image/jpeg");

        // Act
        var response = await client.PostAsJsonAsync("/api/profiles/me/photograph", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.ProfileRepository.DidNotReceive().UpdatePhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
