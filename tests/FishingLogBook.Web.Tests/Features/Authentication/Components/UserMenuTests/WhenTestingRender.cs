using AwesomeAssertions;
using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Web.Features.Authentication.Components.UserMenu;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Authentication.Components.UserMenuTests;

public class WhenTestingRender : BaseUserMenuTest
{
    [Fact]
    public async Task ItShouldOfferSignInAndCreateAccountWhenUnauthenticated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileSummary = ProfileSummary();
        await using var context = CreateContext(profileSummary, isAuthenticated: false);

        // Act
        var cut = context.Render<UserMenu>();

        // Assert
        cut.Find("#auth-sign-in-button").TextContent.Should().Contain("Sign in");
        cut.Find("#auth-create-account-button").TextContent.Should().Contain("Create account");
        cut.FindAll("#user-menu-button").Should().BeEmpty();
        await profileSummary.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateTheMenuStraightFromTheAvatarButton()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileSummary = ProfileSummary();
        await using var context = CreateContext(profileSummary);

        // Act
        var cut = context.Render<UserMenu>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.FindAll("#user-menu > .mud-menu-activator > #user-menu-button").Should().HaveCount(1));
        await profileSummary.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFallBackToTheDefaultAvatarWhenThereIsNoPhotograph()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileSummary = ProfileSummary();
        await using var context = CreateContext(profileSummary);

        // Act
        var cut = context.Render<UserMenu>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#user-menu-default-avatar").Should().NotBeNull());
        cut.FindAll("#user-menu-photograph").Should().BeEmpty();
        cut.Find("#user-menu-button").GetAttribute("aria-label").Should().Be("Account menu");
        cut.Find("#user-menu-button").GetAttribute("title").Should().Be(SignedInEmail);
        await profileSummary.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheDefaultAvatarWhenTheProfileFailsToLoad()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileSummary = Substitute.For<IProfileSummaryProvider>();
        profileSummary.GetAsync(Arg.Any<CancellationToken>()).Returns(ProfileSummaryModel.Empty);
        await using var context = CreateContext(profileSummary);

        // Act
        var cut = context.Render<UserMenu>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#user-menu-default-avatar").Should().NotBeNull());
        cut.FindAll("#user-menu-photograph").Should().BeEmpty();
        await profileSummary.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderBeforeTheProfileSummaryResolves()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var pending = new TaskCompletionSource<ProfileSummaryModel>();
        var profileSummary = Substitute.For<IProfileSummaryProvider>();
        profileSummary.GetAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);
        await using var context = CreateContext(profileSummary);

        // Act
        var cut = context.Render<UserMenu>();

        // Assert
        cut.Find("#user-menu-default-avatar").Should().NotBeNull();
        cut.FindAll("#user-menu-photograph").Should().BeEmpty();
        pending.SetResult(WithPhotograph());
        cut.WaitForAssertion(() => cut.Find("#user-menu-photograph").Should().NotBeNull());
    }

    [Fact]
    public async Task ItShouldUseTheProfilePhotographWhenOneIsAvailable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileSummary = ProfileSummary(WithPhotograph());
        await using var context = CreateContext(profileSummary);

        // Act
        var cut = context.Render<UserMenu>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#user-menu-photograph").Should().NotBeNull();
            cut.Find("#user-menu-photograph img").GetAttribute("src")
                .Should().Be("https://cdn.test/photo.jpg");
            cut.Find("#user-menu-button").GetAttribute("title").Should().Be("Eamonn");
        });
        cut.FindAll("#user-menu-default-avatar").Should().BeEmpty();
        await profileSummary.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheUpdatedPhotographWhenTheSummaryChanges()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileSummary = ProfileSummary();
        await using var context = CreateContext(profileSummary);
        var cut = context.Render<UserMenu>();
        cut.WaitForAssertion(() => cut.Find("#user-menu-default-avatar").Should().NotBeNull());
        profileSummary.GetAsync(Arg.Any<CancellationToken>()).Returns(WithPhotograph());

        // Act
        profileSummary.Changed += Raise.Event<Action>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#user-menu-photograph img").GetAttribute("src")
                .Should().Be("https://cdn.test/photo.jpg"));
        cut.FindAll("#user-menu-default-avatar").Should().BeEmpty();
        await profileSummary.Received(2).GetAsync(Arg.Any<CancellationToken>());
    }
}
