using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.AnglerLookupServiceTests;

public class WhenTestingFind : BaseAnglerLookupServiceTest
{
    [Fact]
    public async Task ItShouldRefuseAnEmptyQuery()
    {
        // Act
        var result = await Sut.FindAsync(Args(string.Empty), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<AnglerLookupQueryInvalidError>();
        await MockProfileRepository.DidNotReceive().FindAnglersAsync(
            Arg.Any<FindAnglersArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAQueryOneCharacterUnderTheMinimum()
    {
        // Act
        var result = await Sut.FindAsync(
            Args(new string('a', AnglerLookupConstants.MinQueryLength - 1)),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<AnglerLookupQueryInvalidError>();
        await MockProfileRepository.DidNotReceive().FindAnglersAsync(
            Arg.Any<FindAnglersArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAQueryOneCharacterOverTheMaximum()
    {
        // Act
        var result = await Sut.FindAsync(
            Args(new string('a', AnglerLookupConstants.MaxQueryLength + 1)),
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<AnglerLookupQueryInvalidError>();
        await MockProfileRepository.DidNotReceive().FindAnglersAsync(
            Arg.Any<FindAnglersArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefuseAQueryThatIsOnlyWhitespace()
    {
        // Act
        var result = await Sut.FindAsync(Args("      "), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<AnglerLookupQueryInvalidError>();
        await MockProfileRepository.DidNotReceive().FindAnglersAsync(
            Arg.Any<FindAnglersArgs>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheRepositoryFailure()
    {
        // Arrange
        MockProfileRepository.FindAnglersAsync(Arg.Any<FindAnglersArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<AnglerSummary>>("Failed to load angler profile."));

        // Act
        var result = await Sut.FindAsync(Args(), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
    }

    [Fact]
    public async Task ItShouldTrimTheQueryAndExcludeTheRequestingAngler()
    {
        // Act
        var result = await Sut.FindAsync(Args("  John Connolly  "), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockProfileRepository.Received(1).FindAnglersAsync(
            Arg.Is<FindAnglersArgs>(args =>
                args.Query == "John Connolly"
                && args.RequestingUserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCapTheResultsAtTheLookupMaximum()
    {
        // Act
        var result = await Sut.FindAsync(Args(maxResults: 500), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await MockProfileRepository.Received(1).FindAnglersAsync(
            Arg.Is<FindAnglersArgs>(args => args.MaxResults == AnglerLookupConstants.MaxResults),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOnlyExposeTheSafeProfileFieldsAndEmail()
    {
        // Arrange
        var properties = typeof(AnglerSummaryDto).GetProperties().Select(property => property.Name);

        // Assert
        properties.Should().BeEquivalentTo(["UserId", "DisplayName", "PhotographUrl", "HomeRegion", "Email"]);
    }

    [Fact]
    public async Task ItShouldIncludeTheEmailSoTheClientCanFallBackToItWhenThereIsNoDisplayName()
    {
        // Arrange
        MockProfileRepository.FindAnglersAsync(Arg.Any<FindAnglersArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<AnglerSummary>>(
                [Angler(displayName: null, email: "angler@example.test")]));

        // Act
        var result = await Sut.FindAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].DisplayName.Should().BeNull();
        result.Value[0].Email.Should().Be("angler@example.test");
    }

    [Fact]
    public async Task ItShouldReturnOnlyTheSafeProfileFields()
    {
        // Arrange
        MockProfileRepository.FindAnglersAsync(Arg.Any<FindAnglersArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<AnglerSummary>>(
                [Angler(photographObjectKey: "profiles/photo.jpg", homeRegion: "Galway")]));

        // Act
        var result = await Sut.FindAsync(Args(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].UserId.Should().Be(MatchedUserId);
        result.Value[0].DisplayName.Should().Be("John Connolly");
        result.Value[0].HomeRegion.Should().Be("Galway");
        result.Value[0].PhotographUrl.Should().Be("https://storage.test/profiles/photo.jpg?signed=1");
        await MockObjectStorage.Received(1).CreateDownloadUrlAsync(
            "profiles/photo.jpg",
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitThePhotographAndRegionTheAnglerKeepsPrivate()
    {
        // Arrange
        MockProfileRepository.FindAnglersAsync(Arg.Any<FindAnglersArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<AnglerSummary>>([Angler()]));

        // Act
        var result = await Sut.FindAsync(Args(), CancellationToken.None);

        // Assert
        result.Value[0].PhotographUrl.Should().BeNull();
        result.Value[0].HomeRegion.Should().BeNull();
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    private static FindAnglersArgs Args(string query = "John Connolly", int maxResults = 0)
    {
        return new FindAnglersArgs
        {
            RequestingUserId = CurrentUserId,
            Query = query,
            MaxResults = maxResults
        };
    }
}
