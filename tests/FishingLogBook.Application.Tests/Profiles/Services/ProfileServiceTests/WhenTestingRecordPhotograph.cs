using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class WhenTestingRecordPhotograph : BaseProfileServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheObjectKeyDoesNotMatchTheAuthenticatedUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var args = new RecordProfilePhotographArgs
        {
            UserId = userId,
            PhotographId = photographId,
            ObjectKey = $"profiles/{otherUserId:D}/{photographId:D}",
            ContentType = PhotographContentTypeConstants.Jpeg
        };

        // Act
        var result = await Sut.RecordPhotographAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Photograph object key does not match the profile.");
        await MockProfileRepository.DidNotReceive().UpdatePhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheRepositoryUpdateFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var args = ValidArgs(userId, photographId, objectKey);
        MockProfileRepository
            .UpdatePhotographAsync(
                userId,
                photographId,
                objectKey,
                PhotographContentTypeConstants.Jpeg,
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile>("Angler profile was not found."));

        // Act
        var result = await Sut.RecordPhotographAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Angler profile was not found.");
        await MockProfileRepository.Received(1).UpdatePhotographAsync(
            userId,
            photographId,
            objectKey,
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnANullPhotographUrlWhenObjectStorageIsUnavailable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var args = ValidArgs(userId, photographId, objectKey);
        MockObjectStorage.IsConfigured.Returns(false);
        MockProfileRepository
            .UpdatePhotographAsync(
                userId,
                photographId,
                objectKey,
                PhotographContentTypeConstants.Jpeg,
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok(RecordedProfile(userId, photographId, objectKey)));

        // Act
        var result = await Sut.RecordPhotographAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PhotographId.Should().Be(photographId);
        result.Value.PhotographUrl.Should().BeNull();
        result.Value.PhotographContentType.Should().Be(PhotographContentTypeConstants.Jpeg);
        await MockProfileRepository.Received(1).UpdatePhotographAsync(
            userId,
            photographId,
            objectKey,
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordThePhotographOnTheAuthenticatedUsersProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var args = ValidArgs(userId, photographId, objectKey);
        MockProfileRepository
            .UpdatePhotographAsync(
                userId,
                photographId,
                objectKey,
                PhotographContentTypeConstants.Jpeg,
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok(RecordedProfile(userId, photographId, objectKey)));
        MockObjectStorage
            .CreateDownloadUrlAsync(objectKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/download"));

        // Act
        var result = await Sut.RecordPhotographAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.PhotographId.Should().Be(photographId);
        result.Value.PhotographUrl.Should().Be("https://storage.test/download");
        result.Value.PhotographContentType.Should().Be(PhotographContentTypeConstants.Jpeg);
        await MockProfileRepository.Received(1).UpdatePhotographAsync(
            userId,
            photographId,
            objectKey,
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
        await MockObjectStorage.Received(1).CreateDownloadUrlAsync(
            objectKey,
            TimeSpan.FromHours(1),
            Arg.Any<CancellationToken>());
    }

    private static RecordProfilePhotographArgs ValidArgs(Guid userId, Guid photographId, string objectKey)
    {
        return new RecordProfilePhotographArgs
        {
            UserId = userId,
            PhotographId = photographId,
            ObjectKey = objectKey,
            ContentType = PhotographContentTypeConstants.Jpeg
        };
    }

    private static Profile RecordedProfile(Guid userId, Guid photographId, string objectKey)
    {
        return new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .WithPhotograph(photographId, objectKey, PhotographContentTypeConstants.Jpeg)
            .Build();
    }
}
