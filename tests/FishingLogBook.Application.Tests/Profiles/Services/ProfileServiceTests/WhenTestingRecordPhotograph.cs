using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class WhenTestingRecordPhotograph : BaseProfileServiceTest
{
    [Fact]
    public async Task ItShouldFailWhenTheObjectKeyDoesNotMatchTheProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var args = new RecordProfilePhotographArgs
        {
            UserId = userId,
            PhotographId = photographId,
            ObjectKey = "profiles/other/photo",
            ContentType = "image/jpeg"
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
    }

    [Fact]
    public async Task ItShouldRecordThePhotographOnTheProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var args = new RecordProfilePhotographArgs
        {
            UserId = userId,
            PhotographId = photographId,
            ObjectKey = objectKey,
            ContentType = "image/jpeg"
        };
        var saved = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Eamonn")
            .Build();
        saved = new Profile
        {
            UserId = saved.UserId,
            DisplayName = saved.DisplayName,
            PhotographId = photographId,
            PhotographObjectKey = objectKey,
            PhotographContentType = "image/jpeg"
        };
        MockProfileRepository
            .UpdatePhotographAsync(userId, photographId, objectKey, "image/jpeg", Arg.Any<CancellationToken>())
            .Returns(Result.Ok(saved));
        MockObjectStorage
            .CreateDownloadUrlAsync(objectKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/download"));

        // Act
        var result = await Sut.RecordPhotographAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PhotographId.Should().Be(photographId);
        result.Value.PhotographUrl.Should().Be("https://storage.test/download");
        await MockProfileRepository.Received(1).UpdatePhotographAsync(
            userId,
            photographId,
            objectKey,
            "image/jpeg",
            Arg.Any<CancellationToken>());
        await MockObjectStorage.Received(1).CreateDownloadUrlAsync(
            objectKey,
            TimeSpan.FromHours(1),
            Arg.Any<CancellationToken>());
    }
}
