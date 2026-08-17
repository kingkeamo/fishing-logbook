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
    public async Task ItShouldShowLoadFailureWhenTheClientFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetPublicAsync(userId, Arg.Any<CancellationToken>())
            .Returns<PublicProfileDto>(_ => throw new HttpRequestException("Not found"));
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<FishingLogBook.Web.Features.Profile.Pages.PublicProfile.PublicProfile>(
            parameters => parameters.Add(profile => profile.UserId, userId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#public-profile-load-failed").TextContent.Should().Contain("Unable to load profile."));
        await profileClient.Received(1).GetPublicAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRenderHiddenFieldsFromAnEmptyPublicDto()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetPublicAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new PublicProfileDto(userId, null, null, null, [], []));
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<FishingLogBook.Web.Features.Profile.Pages.PublicProfile.PublicProfile>(
            parameters => parameters.Add(profile => profile.UserId, userId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#public-profile-card").Should().NotBeNull();
            cut.FindAll("#public-profile-display-name").Should().BeEmpty();
            cut.FindAll("#public-profile-photo").Should().BeEmpty();
            cut.FindAll("#public-profile-home-region").Should().BeEmpty();
            cut.FindAll("#public-profile-fishing-types").Should().BeEmpty();
            cut.FindAll("#public-profile-preferred-species").Should().BeEmpty();
            cut.FindAll("#public-profile-location").Should().BeEmpty();
        });
        await profileClient.Received(1).GetPublicAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchPublicProfileCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetPublicAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new PublicProfileDto(userId, "Eamonn", null, "Westmeath", [], []));
        await using var context = CreateContext(profileClient);

        // Act
        var cut = context.Render<FishingLogBook.Web.Features.Profile.Pages.PublicProfile.PublicProfile>(
            parameters => parameters.Add(profile => profile.UserId, userId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Profil pêcheur");
            cut.Find("#public-profile-display-name").TextContent.Should().Contain("Eamonn");
        });
        await profileClient.Received(1).GetPublicAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderVisiblePublicProfileFields()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetPublicAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new PublicProfileDto(
                userId,
                "Eamonn",
                "https://storage.test/photo",
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
            cut.Markup.Should().Contain("Angler profile");
            cut.Find("#public-profile-display-name").TextContent.Should().Contain("Eamonn");
            cut.Find("#public-profile-photo").GetAttribute("src").Should().Be("https://storage.test/photo");
            cut.Find("#public-profile-home-region").TextContent.Should().Contain("Westmeath");
            cut.Find("#public-profile-fishing-types").TextContent.Should().Contain("Fly");
            cut.Find("#public-profile-preferred-species").TextContent.Should().Contain("Pike");
            cut.FindAll("#public-profile-location").Should().BeEmpty();
        });
        await profileClient.Received(1).GetPublicAsync(userId, Arg.Any<CancellationToken>());
    }
}
