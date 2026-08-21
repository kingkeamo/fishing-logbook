using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Onboarding.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;
using OnboardingPage = FishingLogBook.Web.Features.Onboarding.Pages.Onboarding.Onboarding;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.OnboardingTests;

public class WhenTestingPreferences
{
    private static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid SpinningMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    [Fact]
    public async Task ItShouldUseTheReusableMultiSelectWithExistingMethodsSelected()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var modal = Substitute.For<IModalService>();
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(), Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult(
            [
                new CatalogueOptionModel(FlyMethodId, "Fly", "Fly"),
                new CatalogueOptionModel(SpinningMethodId, "Spinning", "Spinning")
            ]));
        await using var context = CreateContext(modal);
        var cut = context.Render<OnboardingPage>();
        cut.WaitForAssertion(() => cut.Find("#onboarding-next"));
        await cut.Find("#onboarding-next").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#onboarding-method-more"));

        // Act
        await cut.Find("#onboarding-method-more").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#onboarding-method-Spinning").ClassList.Should().Contain("mud-chip-filled"));
        await modal.Received(1)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Is<CataloguePickerModalModel>(model =>
                    model.AllowMultiple
                    && model.Options.Count == 2
                    && model.SelectedOptionIds != null
                    && model.SelectedOptionIds.Contains(FlyMethodId)),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireAtLeastOneFishingMethod()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(new FishingPreferencesDto([]));
        var cut = fixture.Context.Render<OnboardingPage>();

        // Act
        await MoveToPreferencesAsync(cut);
        await cut.Find("#onboarding-next").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#onboarding-preference-validation").TextContent
                .Should().Contain("Choose at least one fishing method."));
        cut.Find("#onboarding-method-chips").Should().NotBeNull();
        await fixture.Preferences.DidNotReceive().UpdatePreferencesAsync(
            Arg.Any<UpdateFishingPreferencesDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireAtLeastOneSpeciesForTheSelectedMethod()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(PreferencesWithoutSpecies());
        var cut = fixture.Context.Render<OnboardingPage>();

        // Act
        await MoveToPreferencesAsync(cut);
        await cut.Find("#onboarding-next").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#onboarding-preference-validation").TextContent
                .Should().Contain("Choose at least one species for Fly."));
        await fixture.Preferences.DidNotReceive().UpdatePreferencesAsync(
            Arg.Any<UpdateFishingPreferencesDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIdentifyASelectedMethodWithoutSpecies()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(TwoMethodPreferences(spinningHasSpecies: false));
        var cut = fixture.Context.Render<OnboardingPage>();

        // Act
        await MoveToPreferencesAsync(cut);
        await cut.Find("#onboarding-next").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#onboarding-preference-validation").TextContent
                .Should().Contain("Choose at least one species for Spinning."));
        await fixture.Preferences.DidNotReceive().UpdatePreferencesAsync(
            Arg.Any<UpdateFishingPreferencesDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldListAllSelectedMethodsWithoutSpecies()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preferences = new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(FlyMethodId, "Fly", "Fly", true, []),
            new FishingMethodPreferenceDto(SpinningMethodId, "Spinning", "Spinning", false, [])
        ]);
        await using var fixture = CreateFixture(preferences);
        var cut = fixture.Context.Render<OnboardingPage>();

        // Act
        await MoveToPreferencesAsync(cut);
        await cut.Find("#onboarding-next").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#onboarding-preference-validation").TextContent.Should().Contain("Fly, Spinning"));
        await fixture.Preferences.DidNotReceive().UpdatePreferencesAsync(
            Arg.Any<UpdateFishingPreferencesDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAdvanceAndPersistCanonicalPreferenceIdentityWhenRequirementsAreMet()
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
        await fixture.Preferences.Received(1).UpdatePreferencesAsync(
            Arg.Is<UpdateFishingPreferencesDto>(update =>
                update.Methods.Count == 1
                && update.Methods[0].FishingMethodId == FlyMethodId
                && update.Methods[0].Species.Count == 1
                && update.Methods[0].Species[0].SpeciesId == BrownTroutSpeciesId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAdvanceWhenEverySelectedMethodHasSpecies()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(TwoMethodPreferences(spinningHasSpecies: true));
        var cut = fixture.Context.Render<OnboardingPage>();

        // Act
        await MoveToPreferencesAsync(cut);
        await cut.Find("#onboarding-next").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#onboarding-allow-location").Should().NotBeNull());
        await fixture.Preferences.Received(1).UpdatePreferencesAsync(
            Arg.Is<UpdateFishingPreferencesDto>(update =>
                update.Methods.Count == 2
                && update.Methods.All(method => method.Species.Count == 1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheRemoveActionInsideTheSelectedSpeciesPill()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(ValidPreferences());
        var cut = fixture.Context.Render<OnboardingPage>();
        await MoveToPreferencesAsync(cut);

        // Act
        var pill = cut.Find("#onboarding-species-pill-Fly-BrownTrout");
        pill.QuerySelector("#onboarding-species-Fly-BrownTrout").Should().NotBeNull();
        var remove = pill.QuerySelector("#onboarding-species-remove-Fly-BrownTrout");
        remove.Should().NotBeNull();
        remove!.ClassList.Should().Contain("mud-inherit-text");
        await remove.ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.FindAll("#onboarding-species-Fly-BrownTrout").Should().BeEmpty());
    }

    [Fact]
    public async Task ItShouldLocaliseTheIncompleteMethodValidation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var preferences = PreferencesWithoutSpecies("Pêche à la mouche");
        await using var fixture = CreateFixture(preferences);
        var cut = fixture.Context.Render<OnboardingPage>();

        // Act
        await MoveToPreferencesAsync(cut);
        await cut.Find("#onboarding-next").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#onboarding-preference-validation").TextContent
                .Should().Contain("Choisissez au moins une espèce pour Pêche à la mouche."));
        await fixture.Preferences.DidNotReceive().UpdatePreferencesAsync(
            Arg.Any<UpdateFishingPreferencesDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBecomeInvalidWhenTheLastSpeciesIsRemoved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(ValidPreferences());
        var cut = fixture.Context.Render<OnboardingPage>();
        await MoveToPreferencesAsync(cut);

        // Act
        await cut.Find("#onboarding-species-remove-Fly-BrownTrout").ClickAsync();
        await cut.Find("#onboarding-next").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#onboarding-preference-validation").TextContent
                .Should().Contain("Choose at least one species for Fly."));
        await fixture.Preferences.DidNotReceive().UpdatePreferencesAsync(
            Arg.Any<UpdateFishingPreferencesDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowInstallationToBeSkippedAfterRequiredPreferencesAreSaved()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(ValidPreferences());
        var cut = fixture.Context.Render<OnboardingPage>();

        await MoveToPreferencesAsync(cut);
        await cut.Find("#onboarding-next").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#onboarding-skip-location"));
        await cut.Find("#onboarding-skip-location").ClickAsync();
        await cut.Find("#onboarding-next").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-later").Should().NotBeNull());
        await cut.Find("#onboarding-finish").ClickAsync();

        await fixture.Onboarding.Received(1).CompleteAsync(Arg.Any<CancellationToken>());
        fixture.Context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/catches");
        await fixture.Install.DidNotReceive().PromptAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUpdateTheOnboardingInstallStepAfterInstallationSucceeds()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(
            ValidPreferences(),
            installState: new InstallState(false, true, InstallPlatformFamilies.Windows, false),
            installResult: InstallResult.Accepted);
        var cut = fixture.Context.Render<OnboardingPage>();

        await MoveToPreferencesAsync(cut);
        await cut.Find("#onboarding-next").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#onboarding-skip-location"));
        await cut.Find("#onboarding-skip-location").ClickAsync();
        await cut.Find("#onboarding-next").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#install-guidance-action"));
        await cut.Find("#install-guidance-action").ClickAsync();

        cut.WaitForAssertion(() =>
            cut.Find("#install-guidance-installed").TextContent.Should().Contain("installed"));
        await fixture.Install.Received(1).PromptAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRevalidateUsersWhoAlreadyCompletedOnboarding()
    {
        using var culture = TestCulture.Use(CultureNames.English);
        await using var fixture = CreateFixture(new FishingPreferencesDto([]), isCompleted: true);

        fixture.Context.Render<OnboardingPage>();

        fixture.Context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/catches");
        await fixture.Preferences.DidNotReceive().GetPreferencesAsync(Arg.Any<CancellationToken>());
        await fixture.Onboarding.DidNotReceive().CompleteAsync(Arg.Any<CancellationToken>());
    }

    private static BunitContext CreateContext(IModalService modal)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        context.Services.AddSingleton(modal);

        var onboarding = Substitute.For<IOnboardingService>();
        onboarding.IsCompletedAsync(Arg.Any<CancellationToken>()).Returns(false);
        context.Services.AddSingleton(onboarding);

        var profile = Substitute.For<IProfileClient>();
        profile.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(new ProfileDto(
            Guid.NewGuid(), null, null, null, null, null, true, false, false, false, false));
        context.Services.AddSingleton(profile);

        var preferences = Substitute.For<IFishingPreferenceClient>();
        preferences.GetCatalogueAsync(Arg.Any<CancellationToken>()).Returns(new FishingCatalogueDto(
            [
                new FishingMethodDto(FlyMethodId, "Fly", "Fly"),
                new FishingMethodDto(SpinningMethodId, "Spinning", "Spinning")
            ],
            []));
        preferences.GetPreferencesAsync(Arg.Any<CancellationToken>()).Returns(new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(FlyMethodId, "Fly", "Fly", true, [])
        ]));
        context.Services.AddSingleton(preferences);

        var install = Substitute.For<IInstallService>();
        install.GetStateAsync(Arg.Any<CancellationToken>()).Returns(new InstallState(false, false, false, false));
        context.Services.AddSingleton(install);
        context.Services.AddSingleton(Substitute.For<ILocationService>());
        context.AddAuthorization().SetAuthorized("tester@example.test");
        return context;
    }

    private static async Task MoveToPreferencesAsync(IRenderedComponent<OnboardingPage> cut)
    {
        cut.WaitForAssertion(() => cut.Find("#onboarding-next"));
        await cut.Find("#onboarding-next").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#onboarding-method-chips"));
    }

    private static FishingPreferencesDto PreferencesWithoutSpecies(string methodName = "Fly")
    {
        return new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(FlyMethodId, "Fly", methodName, true, [])
        ]);
    }

    private static FishingPreferencesDto ValidPreferences()
    {
        return new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(
                FlyMethodId,
                "Fly",
                "Fly",
                true,
                [new FishingSpeciesPreferenceDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout", true)])
        ]);
    }

    private static FishingPreferencesDto TwoMethodPreferences(bool spinningHasSpecies)
    {
        var species = new FishingSpeciesPreferenceDto(
            BrownTroutSpeciesId, "BrownTrout", "Brown Trout", true);
        return new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(FlyMethodId, "Fly", "Fly", true, [species]),
            new FishingMethodPreferenceDto(
                SpinningMethodId,
                "Spinning",
                "Spinning",
                false,
                spinningHasSpecies ? [species] : [])
        ]);
    }

    private static Fixture CreateFixture(
        FishingPreferencesDto savedPreferences,
        bool isCompleted = false,
        InstallState? installState = null,
        InstallResult installResult = InstallResult.Unavailable)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        context.Services.AddSingleton(Substitute.For<IModalService>());

        var onboarding = Substitute.For<IOnboardingService>();
        onboarding.IsCompletedAsync(Arg.Any<CancellationToken>()).Returns(isCompleted);
        context.Services.AddSingleton(onboarding);

        var profileDto = new ProfileDto(
            Guid.NewGuid(), null, null, null, null, null, true, false, false, false, false);
        var profile = Substitute.For<IProfileClient>();
        profile.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(profileDto);
        profile.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>()).Returns(profileDto);
        context.Services.AddSingleton(profile);

        var preferences = Substitute.For<IFishingPreferenceClient>();
        preferences.GetCatalogueAsync(Arg.Any<CancellationToken>()).Returns(new FishingCatalogueDto(
            [
                new FishingMethodDto(FlyMethodId, "Fly", "Fly"),
                new FishingMethodDto(SpinningMethodId, "Spinning", "Spinning")
            ],
            [new SpeciesDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout")]));
        preferences.GetPreferencesAsync(Arg.Any<CancellationToken>()).Returns(savedPreferences);
        preferences.UpdatePreferencesAsync(
                Arg.Any<UpdateFishingPreferencesDto>(), Arg.Any<CancellationToken>())
            .Returns(savedPreferences);
        context.Services.AddSingleton(preferences);

        var install = Substitute.For<IInstallService>();
        install.GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(installState ?? new InstallState(false, false, InstallPlatformFamilies.Other, false));
        install.PromptAsync(Arg.Any<CancellationToken>()).Returns(installResult);
        context.Services.AddSingleton(install);
        context.Services.AddSingleton(Substitute.For<ILocationService>());
        context.AddAuthorization().SetAuthorized("tester@example.test");
        return new Fixture(context, onboarding, preferences, install);
    }

    private sealed record Fixture(
        BunitContext Context,
        IOnboardingService Onboarding,
        IFishingPreferenceClient Preferences,
        IInstallService Install) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
