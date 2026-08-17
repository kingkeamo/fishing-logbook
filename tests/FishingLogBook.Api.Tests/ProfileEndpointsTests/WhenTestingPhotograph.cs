using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Constants;
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
    public async Task ItShouldRejectUploadUrlWhenAuthorizationIsMissing()
    {
        // Arrange
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ProfileRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();
        var request = new PhotographUploadRequestDto(Guid.NewGuid(), PhotographContentTypeConstants.Jpeg);

        // Act
        var response = await client.PostAsJsonAsync("/api/profiles/me/photograph/upload-url", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
    public async Task ItShouldRejectUploadUrlWhenObjectStorageIsNotConfigured()
    {
        // Arrange
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ObjectStorage.IsConfigured.Returns(false);
        var client = _factory.CreateAuthenticatedClient();
        var request = new PhotographUploadRequestDto(Guid.NewGuid(), PhotographContentTypeConstants.Jpeg);

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
    public async Task ItShouldRejectUploadUrlWhenPhotographIdIsEmpty()
    {
        // Arrange
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ObjectStorage.IsConfigured.Returns(true);
        var client = _factory.CreateAuthenticatedClient();
        var request = new PhotographUploadRequestDto(Guid.Empty, PhotographContentTypeConstants.Jpeg);

        // Act
        var response = await client.PostAsJsonAsync("/api/profiles/me/photograph/upload-url", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
    public async Task ItShouldRejectUploadUrlWhenContentTypeIsNotAllowed()
    {
        // Arrange
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ObjectStorage.IsConfigured.Returns(true);
        var client = _factory.CreateAuthenticatedClient();
        var request = new PhotographUploadRequestDto(Guid.NewGuid(), "image/gif");

        // Act
        var response = await client.PostAsJsonAsync("/api/profiles/me/photograph/upload-url", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
    public async Task ItShouldCreateAnUploadUrlForTheAuthenticatedUsersObjectKey()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
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
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));
        var own = await client.GetFromJsonAsync<ProfileDto>("/api/profiles/me");
        own.Should().NotBeNull();
        _factory.ObjectStorage.ClearReceivedCalls();
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .GetByUserIdAsync(own!.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        var expectedKey = $"profiles/{own.UserId:D}/{photographId:D}";
        var request = new PhotographUploadRequestDto(photographId, PhotographContentTypeConstants.Jpeg);

        // Act
        var response = await client.PostAsJsonAsync("/api/profiles/me/photograph/upload-url", request);
        var body = await response.Content.ReadFromJsonAsync<PhotographUploadDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.UploadUrl.Should().Be("https://storage.test/upload");
        body.ObjectKey.Should().Be(expectedKey);
        await _factory.ObjectStorage.Received(1).CreateUploadUrlAsync(
            expectedKey,
            PhotographContentTypeConstants.Jpeg,
            TimeSpan.FromMinutes(15),
            Arg.Any<CancellationToken>());
        await _factory.ProfileRepository.Received(1).GetByUserIdAsync(
            own.UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectRecordWhenAuthorizationIsMissing()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();
        var request = new RecordPhotographDto(Guid.NewGuid(), "profiles/key", PhotographContentTypeConstants.Jpeg);

        // Act
        var response = await client.PostAsJsonAsync("/api/profiles/me/photograph", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.ProfileRepository.DidNotReceive().UpdatePhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAMismatchedPhotographObjectKey()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        var request = new RecordPhotographDto(
            Guid.NewGuid(),
            $"profiles/{Guid.NewGuid():D}/{Guid.NewGuid():D}",
            PhotographContentTypeConstants.Jpeg);

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

    [Fact]
    public async Task ItShouldRejectRecordWhenContentTypeIsNotAllowed()
    {
        // Arrange
        _factory.ProfileRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        var request = new RecordPhotographDto(Guid.NewGuid(), "profiles/key", "image/gif");

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

    [Fact]
    public async Task ItShouldReturnBadRequestWhenTheRepositoryUpdateFails()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
        _factory.ProfileRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));
        var own = await client.GetFromJsonAsync<ProfileDto>("/api/profiles/me");
        own.Should().NotBeNull();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{own!.UserId:D}/{photographId:D}";
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .UpdatePhotographAsync(
                own.UserId,
                photographId,
                objectKey,
                PhotographContentTypeConstants.Png,
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile>("Angler profile was not found."));
        var request = new RecordPhotographDto(photographId, objectKey, PhotographContentTypeConstants.Png);

        // Act
        var response = await client.PostAsJsonAsync("/api/profiles/me/photograph", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.ProfileRepository.Received(1).UpdatePhotographAsync(
            own.UserId,
            photographId,
            objectKey,
            PhotographContentTypeConstants.Png,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordThePhotographForTheAuthenticatedUser()
    {
        // Arrange
        var subject = Guid.NewGuid().ToString("N");
        _factory.ObjectStorage.IsConfigured.Returns(true);
        _factory.ProfileRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));
        var own = await client.GetFromJsonAsync<ProfileDto>("/api/profiles/me");
        own.Should().NotBeNull();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{own!.UserId:D}/{photographId:D}";
        _factory.ProfileRepository.ClearReceivedCalls();
        _factory.ProfileRepository
            .UpdatePhotographAsync(
                own.UserId,
                photographId,
                objectKey,
                PhotographContentTypeConstants.Jpeg,
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new Profile
            {
                UserId = own.UserId,
                PhotographId = photographId,
                PhotographObjectKey = objectKey,
                PhotographContentType = PhotographContentTypeConstants.Jpeg
            }));
        _factory.ObjectStorage
            .CreateDownloadUrlAsync(objectKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/download"));
        var request = new RecordPhotographDto(photographId, objectKey, PhotographContentTypeConstants.Jpeg);

        // Act
        var response = await client.PostAsJsonAsync("/api/profiles/me/photograph", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(own.UserId);
        body.PhotographId.Should().Be(photographId);
        await _factory.ProfileRepository.Received(1).UpdatePhotographAsync(
            own.UserId,
            photographId,
            objectKey,
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
    }
}
