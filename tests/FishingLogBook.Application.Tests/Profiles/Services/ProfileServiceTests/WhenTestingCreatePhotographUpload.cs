using AwesomeAssertions;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class WhenTestingCreatePhotographUpload : BaseProfileServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheProfileCannotBeLoaded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new PhotographUploadRequestDto(Guid.NewGuid(), "image/jpeg");
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile?>("Failed to load angler profile."));

        // Act
        var result = await Sut.CreatePhotographUploadAsync(userId, request, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
        await MockObjectStorage.DidNotReceive().CreateUploadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCreateAnUploadUrlForTheProfilePhotograph()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var request = new PhotographUploadRequestDto(photographId, "image/jpeg");
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(new ProfileBuilder().WithUserId(userId).Build()));
        MockObjectStorage
            .CreateUploadUrlAsync(objectKey, "image/jpeg", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/upload"));

        // Act
        var result = await Sut.CreatePhotographUploadAsync(userId, request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ObjectKey.Should().Be(objectKey);
        result.Value.UploadUrl.Should().Be("https://storage.test/upload");
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await MockObjectStorage.Received(1).CreateUploadUrlAsync(
            objectKey,
            "image/jpeg",
            TimeSpan.FromMinutes(15),
            Arg.Any<CancellationToken>());
    }
}
