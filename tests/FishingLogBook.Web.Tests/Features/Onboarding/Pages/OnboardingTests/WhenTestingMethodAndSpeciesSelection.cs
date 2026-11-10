using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common.Modals;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OnboardingPage = FishingLogBook.Web.Features.Onboarding.Pages.Onboarding.Onboarding;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.OnboardingTests;

public class WhenTestingMethodAndSpeciesSelection : BaseOnboardingTest
{
    private static IModalService ModalOf(Fixture fixture)
    {
        return fixture.Context.Services.GetRequiredService<IModalService>();
    }

    [Fact]
    public async Task ItShouldKeepTheSelectionWhenTheMethodPickerIsDismissed()
    {
        // Arrange
        await using var fixture = CreateFixture(ValidPreferences());
        var modal = ModalOf(fixture);
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CataloguePickerModalResult?>(null));
        var cut = fixture.Context.Render<OnboardingPage>();
        await MoveToPreferencesAsync(cut);

        // Act
        await cut.Find("#onboarding-method-more").ClickAsync();

        // Assert
        cut.Find("#onboarding-method-Fly").ClassList.Should().Contain("mud-chip-filled");
        cut.Find("#onboarding-method-Spinning").ClassList.Should().NotContain("mud-chip-filled");
        await modal.Received(1).ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            Arg.Any<CataloguePickerModalModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDropADeselectedMethodAndMakeTheRemainingOneTheDefault()
    {
        // Arrange
        await using var fixture = CreateFixture(ValidPreferences());
        var modal = ModalOf(fixture);
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(), Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult([new CatalogueOptionModel(SpinningMethodId, "Spinning", "Spinning")]));
        var cut = fixture.Context.Render<OnboardingPage>();
        await MoveToPreferencesAsync(cut);

        // Act
        await cut.Find("#onboarding-method-more").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#onboarding-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#onboarding-method-Fly").ClassList.Should().NotContain("mud-chip-filled");
            cut.FindAll("[id^='onboarding-method-default-']").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldToggleAMethodOnAndOffAndMoveTheDefaultBetweenSelectedMethods()
    {
        // Arrange
        await using var fixture = CreateFixture(ValidPreferences());
        var cut = fixture.Context.Render<OnboardingPage>();
        await MoveToPreferencesAsync(cut);

        // Act
        await cut.Find("#onboarding-method-Spinning").ClickAsync();

        // Assert
        cut.Find("#onboarding-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
        cut.Find("#onboarding-method-default-Fly").ClassList.Should().Contain("mud-chip-filled");
        cut.Find("#onboarding-method-default-Spinning").ClassList.Should().NotContain("mud-chip-filled");

        // Act
        await cut.Find("#onboarding-method-default-Spinning").ClickAsync();

        // Assert
        cut.Find("#onboarding-method-default-Spinning").ClassList.Should().Contain("mud-chip-filled");
        cut.Find("#onboarding-method-default-Fly").ClassList.Should().NotContain("mud-chip-filled");

        // Act
        await cut.Find("#onboarding-method-Spinning").ClickAsync();

        // Assert
        cut.Find("#onboarding-method-Spinning").ClassList.Should().NotContain("mud-chip-filled");
        cut.Find("#onboarding-method-Fly").ClassList.Should().Contain("mud-chip-filled");
        cut.FindAll("[id^='onboarding-method-default-']").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepTheSpeciesWhenTheSpeciesPickerIsDismissed()
    {
        // Arrange
        await using var fixture = CreateFixture(ValidPreferences());
        var modal = ModalOf(fixture);
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CataloguePickerModalResult?>(null));
        var cut = fixture.Context.Render<OnboardingPage>();
        await MoveToPreferencesAsync(cut);

        // Act
        await cut.Find("#onboarding-species-more-Fly").ClickAsync();

        // Assert
        cut.Find("#onboarding-species-pill-Fly-BrownTrout").Should().NotBeNull();
        await modal.Received(1).ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            Arg.Is<CataloguePickerModalModel>(model => model.AllowMultiple
                && model.SelectedOptionIds != null
                && model.SelectedOptionIds.Contains(BrownTroutSpeciesId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemoveSpeciesThatAreDeselectedInThePicker()
    {
        // Arrange
        await using var fixture = CreateFixture(ValidPreferences());
        var modal = ModalOf(fixture);
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(), Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult([]));
        var cut = fixture.Context.Render<OnboardingPage>();
        await MoveToPreferencesAsync(cut);

        // Act
        await cut.Find("#onboarding-species-more-Fly").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#onboarding-species-pill-Fly-BrownTrout").Should().BeEmpty());
    }

    [Fact]
    public async Task ItShouldRemoveASelectedSpeciesFromItsPill()
    {
        // Arrange
        await using var fixture = CreateFixture(ValidPreferences());
        var cut = fixture.Context.Render<OnboardingPage>();
        await MoveToPreferencesAsync(cut);

        // Act
        await cut.Find("#onboarding-species-remove-Fly-BrownTrout").ClickAsync();

        // Assert
        cut.FindAll("#onboarding-species-pill-Fly-BrownTrout").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldMakeASpeciesTheDefaultWhenItsPillIsChosen()
    {
        // Arrange
        await using var fixture = CreateFixture(ValidPreferences());
        var cut = fixture.Context.Render<OnboardingPage>();
        await MoveToPreferencesAsync(cut);

        // Act
        await cut.Find("#onboarding-species-Fly-BrownTrout").ClickAsync();

        // Assert
        cut.Find("#onboarding-species-pill-Fly-BrownTrout").Should().NotBeNull();
    }
}
