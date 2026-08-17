using AwesomeAssertions;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class WhenTestingGetPublic : BaseProfileServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenUserExistenceLookupFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<bool>("Failed to load angler profile."));

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
        await MockProfileRepository.Received(1).UserExistsAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnNotFoundWhenTheUserDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(false));

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.HasError<ProfileNotFoundError>().Should().BeTrue();
        result.Errors[0].Message.Should().Be("Angler profile was not found.");
        await MockProfileRepository.Received(1).UserExistsAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().GetByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheProfileLookupFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile?>("Failed to load angler profile."));

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnADefaultPublicProfileWithoutWritingWhenNoProfileRowExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        MockProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.DisplayName.Should().BeNull();
        result.Value.PhotographUrl.Should().BeNull();
        result.Value.HomeRegion.Should().BeNull();
        result.Value.PreferredFishingTypes.Should().BeEmpty();
        result.Value.PreferredSpecies.Should().BeEmpty();
        typeof(PublicProfileDto).GetProperty("Latitude").Should().BeNull();
        typeof(PublicProfileDto).GetProperty("Longitude").Should().BeNull();
        typeof(PublicProfileDto).GetProperty("Location").Should().BeNull();
        await MockProfileRepository.Received(1).UserExistsAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitDisplayNameWhenShowDisplayNameIsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        StubVisibleProfile(userId, profile => profile.HideDisplayName());

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().BeNull();
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.PreferredFishingTypes.Should().Equal("Fly");
        result.Value.PreferredSpecies.Should().Equal("Pike");
        result.Value.PhotographUrl.Should().Be("https://storage.test/download");
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitPhotographWhenShowPhotographIsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        StubVisibleProfile(userId, profile => profile.HidePhotograph());

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PhotographUrl.Should().BeNull();
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.PreferredFishingTypes.Should().Equal("Fly");
        result.Value.PreferredSpecies.Should().Equal("Pike");
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitHomeRegionWhenShowHomeRegionIsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        StubVisibleProfile(userId, profile => profile.HideHomeRegion());

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HomeRegion.Should().BeNull();
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.PhotographUrl.Should().Be("https://storage.test/download");
        result.Value.PreferredFishingTypes.Should().Equal("Fly");
        result.Value.PreferredSpecies.Should().Equal("Pike");
    }

    [Fact]
    public async Task ItShouldOmitFishingTypesWhenShowPreferredFishingTypesIsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        StubVisibleProfile(userId, profile => profile.HideFishingTypes());

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PreferredFishingTypes.Should().BeEmpty();
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.PreferredSpecies.Should().Equal("Pike");
    }

    [Fact]
    public async Task ItShouldOmitSpeciesWhenShowPreferredSpeciesIsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        StubVisibleProfile(userId, profile => profile.HideSpecies());

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PreferredSpecies.Should().BeEmpty();
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.PreferredFishingTypes.Should().Equal("Fly");
    }

    [Fact]
    public async Task ItShouldOmitEveryHiddenFieldWhenAllVisibilityFlagsAreOff()
    {
        // Arrange
        var userId = Guid.NewGuid();
        StubVisibleProfile(userId, profile => profile.HideAll());

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().BeNull();
        result.Value.PhotographUrl.Should().BeNull();
        result.Value.HomeRegion.Should().BeNull();
        result.Value.PreferredFishingTypes.Should().BeEmpty();
        result.Value.PreferredSpecies.Should().BeEmpty();
        typeof(PublicProfileDto).GetProperty("Location").Should().BeNull();
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIncludeEachFieldWhenItsVisibilityFlagIsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var objectKey = StubVisibleProfile(userId, profile => profile);

        // Act
        var result = await Sut.GetPublicAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.PhotographUrl.Should().Be("https://storage.test/download");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.PreferredFishingTypes.Should().Equal("Fly");
        result.Value.PreferredSpecies.Should().Equal("Pike");
        typeof(PublicProfileDto).GetProperty("Latitude").Should().BeNull();
        typeof(PublicProfileDto).GetProperty("Longitude").Should().BeNull();
        typeof(PublicProfileDto).GetProperty("Location").Should().BeNull();
        await MockProfileRepository.Received(1).UserExistsAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await MockObjectStorage.Received(1).CreateDownloadUrlAsync(
            objectKey,
            TimeSpan.FromHours(1),
            Arg.Any<CancellationToken>());
    }

    private string StubVisibleProfile(Guid userId, Func<ProfileBuilder, ProfileBuilder> configure)
    {
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var profile = configure(
                new ProfileBuilder()
                    .WithUserId(userId)
                    .WithDisplayName("Eamonn")
                    .WithPhotograph(photographId, objectKey, PhotographContentTypeConstants.Jpeg)
                    .WithHomeRegion("Westmeath")
                    .WithFishingTypes("Fly")
                    .WithSpecies("Pike")
                    .ShowAll())
            .Build();
        MockProfileRepository
            .UserExistsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(profile));
        MockObjectStorage
            .CreateDownloadUrlAsync(objectKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/download"));
        return objectKey;
    }
}
