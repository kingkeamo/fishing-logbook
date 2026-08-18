using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Tests.Common.Builders;
using FluentResults;
using NSubstitute;
using DomainLengthUnitEnum = FishingLogBook.Domain.Enums.LengthUnitEnum;
using DomainWeightUnitEnum = FishingLogBook.Domain.Enums.WeightUnitEnum;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class WhenTestingUpdateOwn : BaseProfileServiceTest
{
    [Fact]
    public async Task ItShouldReturnFailureWhenTheLookupFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var args = ValidArgs(userId);
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile?>("Failed to load angler profile."));

        // Act
        var result = await Sut.UpdateOwnAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Profile>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenUpsertFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var args = ValidArgs(userId);
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        MockProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Profile>("Failed to load angler profile."));

        // Act
        var result = await Sut.UpdateOwnAsync(args, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load angler profile.");
        await MockProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile => profile.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTrimDisplayNameHomeRegionAndSpecies()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var args = new UpdateProfileArgs
        {
            UserId = userId,
            DisplayName = "  Eamonn  ",
            HomeRegion = "  Westmeath  ",
            PreferredFishingTypes = ["Fly"],
            PreferredSpecies = ["  Pike  ", " ", "Tench"],
            ShowDisplayName = true
        };
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        MockProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Profile>(0)));

        // Act
        var result = await Sut.UpdateOwnAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.PreferredSpecies.Should().Equal("Pike", "Tench");
        await MockProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile =>
                profile.UserId == userId
                && profile.DisplayName == "Eamonn"
                && profile.HomeRegion == "Westmeath"
                && profile.PreferredSpecies.SequenceEqual(new[] { "Pike", "Tench" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreservePhotographFieldsWhenUpdatingEditableProfileValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var objectKey = $"profiles/{userId:D}/{photographId:D}";
        var existing = new ProfileBuilder()
            .WithUserId(userId)
            .WithDisplayName("Old")
            .WithPhotograph(photographId, objectKey, PhotographContentTypeConstants.Jpeg)
            .Build();
        var args = ValidArgs(userId);
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(existing));
        MockProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Profile>(0)));
        MockObjectStorage
            .CreateDownloadUrlAsync(objectKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/download"));

        // Act
        var result = await Sut.UpdateOwnAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PhotographId.Should().Be(photographId);
        await MockProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile =>
                profile.UserId == userId
                && profile.DisplayName == "Eamonn"
                && profile.PhotographId == photographId
                && profile.PhotographObjectKey == objectKey
                && profile.PhotographContentType == PhotographContentTypeConstants.Jpeg),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistProfileFieldsOnFirstSave()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var args = ValidArgs(userId);
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        MockProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Profile>(0)));

        // Act
        var result = await Sut.UpdateOwnAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.DisplayName.Should().Be("Eamonn");
        result.Value.HomeRegion.Should().Be("Westmeath");
        result.Value.PreferredFishingTypes.Should().Equal("Coarse", "Fly");
        result.Value.PreferredSpecies.Should().Equal("Pike", "Tench");
        result.Value.ShowDisplayName.Should().BeTrue();
        result.Value.ShowPhotograph.Should().BeFalse();
        result.Value.ShowHomeRegion.Should().BeTrue();
        result.Value.ShowPreferredFishingTypes.Should().BeTrue();
        result.Value.ShowPreferredSpecies.Should().BeFalse();
        typeof(ProfileDto).GetProperty("Location").Should().BeNull();
        await MockProfileRepository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
        await MockProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile =>
                profile.UserId == userId
                && profile.DisplayName == "Eamonn"
                && profile.HomeRegion == "Westmeath"
                && profile.PreferredFishingTypes.SequenceEqual(new[] { "Coarse", "Fly" })
                && profile.PreferredSpecies.SequenceEqual(new[] { "Pike", "Tench" })
                && profile.ShowDisplayName
                && !profile.ShowPhotograph
                && profile.ShowHomeRegion
                && profile.ShowPreferredFishingTypes
                && !profile.ShowPreferredSpecies
                && profile.PhotographId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistAndReturnThePreferredMeasurementUnits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var args = new UpdateProfileArgs
        {
            UserId = userId,
            DisplayName = "Eamonn",
            PreferredWeightUnit = DomainWeightUnitEnum.Lb,
            PreferredLengthUnit = DomainLengthUnitEnum.In
        };
        MockProfileRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        MockProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Profile>(0)));

        // Act
        var result = await Sut.UpdateOwnAsync(args, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PreferredWeightUnit.Should().Be(WeightUnitEnum.Lb);
        result.Value.PreferredLengthUnit.Should().Be(LengthUnitEnum.In);
        await MockProfileRepository.Received(1).UpsertAsync(
            Arg.Is<Profile>(profile =>
                profile.UserId == userId
                && profile.PreferredWeightUnit == DomainWeightUnitEnum.Lb
                && profile.PreferredLengthUnit == DomainLengthUnitEnum.In),
            Arg.Any<CancellationToken>());
    }

    private static UpdateProfileArgs ValidArgs(Guid userId)
    {
        return new UpdateProfileArgs
        {
            UserId = userId,
            DisplayName = "Eamonn",
            HomeRegion = "Westmeath",
            PreferredFishingTypes = ["Coarse", "Fly"],
            PreferredSpecies = ["Pike", "Tench"],
            ShowDisplayName = true,
            ShowPhotograph = false,
            ShowHomeRegion = true,
            ShowPreferredFishingTypes = true,
            ShowPreferredSpecies = false
        };
    }
}
