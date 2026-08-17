using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Commands.RecordProfilePhotographCommandTests;

public class WhenTestingHandle : BaseRecordProfilePhotographCommandTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var command = Command(userId, photographId, objectKey);
        MockProfileService
            .RecordPhotographAsync(Arg.Any<RecordProfilePhotographArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<ProfileDto>(new PhotographObjectKeyMismatchError()));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.Error.Should().BeOfType<PhotographObjectKeyMismatchError>();
        response.ErrorMessage.Should().Be("Photograph object key does not match the profile.");
        response.Profile.Should().BeNull();
        await MockProfileService.Received(1).RecordPhotographAsync(
            Arg.Is<RecordProfilePhotographArgs>(args =>
                args.UserId == userId
                && args.PhotographId == photographId
                && args.ObjectKey == objectKey
                && args.ContentType == "image/jpeg"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheUpdatedProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var command = Command(userId, photographId, objectKey);
        var profile = new ProfileDto(
            userId,
            "Eamonn",
            photographId,
            "https://storage.test/download",
            "image/jpeg",
            null,
            [],
            [],
            true,
            true,
            false,
            false,
            false);
        MockProfileService
            .RecordPhotographAsync(Arg.Any<RecordProfilePhotographArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(profile));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Profile.Should().Be(profile);
        await MockProfileService.Received(1).RecordPhotographAsync(
            Arg.Is<RecordProfilePhotographArgs>(args =>
                args.UserId == userId
                && args.PhotographId == photographId
                && args.ObjectKey == objectKey
                && args.ContentType == "image/jpeg"),
            Arg.Any<CancellationToken>());
    }
}
