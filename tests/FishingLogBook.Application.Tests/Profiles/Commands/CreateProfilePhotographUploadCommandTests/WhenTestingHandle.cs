using AwesomeAssertions;
using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Commands.CreateProfilePhotographUploadCommandTests;

public class WhenTestingHandle : BaseCreateProfilePhotographUploadCommandTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenObjectStorageIsNotConfigured()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var command = Command(userId, photographId);
        MockProfileService.IsObjectStorageConfigured.Returns(false);

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<ObjectStorageNotConfiguredError>();
        response.ErrorMessage.Should().Be("Object storage is not configured.");
        response.Upload.Should().BeNull();
        await MockProfileService.DidNotReceive().CreatePhotographUploadAsync(
            Arg.Any<Guid>(),
            Arg.Any<PhotographUploadRequestDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var command = Command(userId, photographId);
        MockProfileService
            .CreatePhotographUploadAsync(userId, command.Request, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<PhotographUploadDto>("Failed to load angler profile."));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("Failed to load angler profile.");
        await MockProfileService.Received(1).CreatePhotographUploadAsync(
            userId,
            Arg.Is<PhotographUploadRequestDto>(request =>
                request.PhotographId == photographId
                && request.ContentType == "image/jpeg"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheUpload()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var command = Command(userId, photographId);
        var upload = new PhotographUploadDto(
            $"profiles/{userId:D}/{photographId:D}",
            "https://storage.test/upload");
        MockProfileService
            .CreatePhotographUploadAsync(userId, command.Request, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(upload));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Upload.Should().Be(upload);
        await MockProfileService.Received(1).CreatePhotographUploadAsync(
            userId,
            Arg.Is<PhotographUploadRequestDto>(request =>
                request.PhotographId == photographId
                && request.ContentType == "image/jpeg"),
            Arg.Any<CancellationToken>());
    }
}
