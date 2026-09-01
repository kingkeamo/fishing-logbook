using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Localization;
using NSubstitute;
using OnboardingPage = FishingLogBook.Web.Features.Onboarding.Pages.Onboarding.Onboarding;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.OnboardingTests;

public class WhenTestingDisplayName : BaseOnboardingTest
{
    [Fact]
    public async Task ItShouldRenderTheDisplayNameHelpOnTheWelcomeStep()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(ValidPreferences());

        // Act
        var cut = fixture.Context.Render<OnboardingPage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#onboarding-display-name").Should().NotBeNull();
            cut.Find("#onboarding-display-name-help").GetAttribute("aria-label")
                .Should().Be("Other anglers may see this name. If left blank, your email address will be used.");
        });
        await fixture.Profile.DidNotReceive().UpdateOwnAsync(
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLocaliseTheDisplayNameHelp()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var fixture = CreateFixture(ValidPreferences());

        // Act
        var cut = fixture.Context.Render<OnboardingPage>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#onboarding-display-name-help").GetAttribute("aria-label")
                .Should().Be("Les autres pêcheurs pourront voir ce nom. S’il est laissé vide, votre adresse e-mail sera utilisée."));
    }

    [Fact]
    public async Task ItShouldSendTheEnteredDisplayNameWhenPreferencesAreSaved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(ValidPreferences());
        var cut = fixture.Context.Render<OnboardingPage>();
        cut.WaitForAssertion(() => cut.Find("#onboarding-display-name"));
        cut.Find("#onboarding-display-name").Input("River Eamonn");

        // Act
        await MoveToPreferencesAsync(cut);
        await cut.Find("#onboarding-next").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#onboarding-allow-location").Should().NotBeNull());
        await fixture.Profile.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile => profile.DisplayName == "River Eamonn"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSendABlankDisplayNameRatherThanApplyingTheEmailFallbackInTheUi()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(ValidPreferences());
        var cut = fixture.Context.Render<OnboardingPage>();

        // Act
        await MoveToPreferencesAsync(cut);
        await cut.Find("#onboarding-next").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#onboarding-allow-location").Should().NotBeNull());
        await fixture.Profile.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                string.IsNullOrWhiteSpace(profile.DisplayName)
                && profile.DisplayName != "tester@example.test"),
            Arg.Any<CancellationToken>());
    }
}
