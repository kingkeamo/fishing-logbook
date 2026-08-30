using AwesomeAssertions;
using FishingLogBook.Domain.Profiles;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.AnglerLookupServiceTests;

public class WhenTestingDescribe : BaseAnglerLookupServiceTest
{
    [Fact]
    public async Task ItShouldNotReadProfilesWhenNoAnglersAreAskedFor()
    {
        // Act
        var result = await Sut.DescribeAsync([], CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await MockProfileRepository.DidNotReceive().GetByUserIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreEmptyAndDuplicateAnglerIds()
    {
        // Act
        var result = await Sut.DescribeAsync(
            [MatchedUserId, MatchedUserId, Guid.Empty],
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockProfileRepository.Received(1).GetByUserIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(userIds =>
                userIds.Count == 1 && userIds.Contains(MatchedUserId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheRepositoryFailure()
    {
        // Arrange
        MockProfileRepository.GetByUserIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<Profile>>("Failed to load angler profile."));

        // Act
        var result = await Sut.DescribeAsync([MatchedUserId], CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
    }

    [Fact]
    public async Task ItShouldHideEveryFieldTheAnglerKeepsPrivate()
    {
        // Arrange
        MockProfileRepository.GetByUserIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Profile>>(
            [
                new Profile
                {
                    UserId = MatchedUserId,
                    DisplayName = "John Connolly",
                    PhotographObjectKey = "profiles/photo.jpg",
                    HomeRegion = "Galway",
                    ShowDisplayName = false,
                    ShowPhotograph = false,
                    ShowHomeRegion = false
                }
            ]));

        // Act
        var result = await Sut.DescribeAsync([MatchedUserId], CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value[MatchedUserId].DisplayName.Should().BeNull();
        result.Value[MatchedUserId].PhotographUrl.Should().BeNull();
        result.Value[MatchedUserId].HomeRegion.Should().BeNull();
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFieldsTheAnglerChoseToShare()
    {
        // Arrange
        MockProfileRepository.GetByUserIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Profile>>(
            [
                new Profile
                {
                    UserId = MatchedUserId,
                    DisplayName = "John Connolly",
                    PhotographObjectKey = "profiles/photo.jpg",
                    HomeRegion = "Galway",
                    ShowDisplayName = true,
                    ShowPhotograph = true,
                    ShowHomeRegion = true
                }
            ]));

        // Act
        var result = await Sut.DescribeAsync([MatchedUserId], CancellationToken.None);

        // Assert
        result.Value[MatchedUserId].DisplayName.Should().Be("John Connolly");
        result.Value[MatchedUserId].HomeRegion.Should().Be("Galway");
        result.Value[MatchedUserId].PhotographUrl.Should().Be("https://storage.test/profiles/photo.jpg?signed=1");
        await MockObjectStorage.Received(1).CreateDownloadUrlAsync(
            "profiles/photo.jpg",
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }
}
