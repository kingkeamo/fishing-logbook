using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.PublicProfileTests;

public class WhenTestingRender : BasePublicProfileTest
{
    [Fact]
    public async Task ItShouldHidePrivateLocationFromAnotherAngler()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetPublicAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new PublicProfileDto(
                userId,
                "Eamonn",
                null,
                "Westmeath",
                ["Fly"],
                ["Pike"]));
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<FishingLogBook.Web.Features.Profile.Pages.PublicProfile.PublicProfile>(
            parameters => parameters.Add(profile => profile.UserId, userId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#public-profile-display-name").TextContent.Should().Contain("Eamonn");
            cut.Find("#public-profile-home-region").TextContent.Should().Contain("Westmeath");
            cut.FindAll("#public-profile-location").Should().BeEmpty();
        });
        await profileClient.Received(1).GetPublicAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowThatPreciseLocationIsSharedWhenTheOwnerChoseToShare()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetPublicAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new PublicProfileDto(
                userId,
                "Eamonn",
                null,
                null,
                [],
                [],
                new CatchLocationDto(
                    53.4,
                    -7.9,
                    8,
                    DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                    LocationDefaults.DeviceGps,
                    LocationDefaults.Public,
                    LocationDefaults.ConsentVersion)));
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<FishingLogBook.Web.Features.Profile.Pages.PublicProfile.PublicProfile>(
            parameters => parameters.Add(profile => profile.UserId, userId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#public-profile-location").TextContent.Should().Contain("Precise location is shared"));
        await profileClient.Received(1).GetPublicAsync(userId, Arg.Any<CancellationToken>());
    }
}
