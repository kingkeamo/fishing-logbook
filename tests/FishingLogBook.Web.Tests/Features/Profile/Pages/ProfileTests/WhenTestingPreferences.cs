using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ProfilePage = FishingLogBook.Web.Features.Profile.Pages.Profile.Profile;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class WhenTestingPreferences : BaseProfileTest
{
    [Fact]
    public async Task ItShouldRenderTheCatalogueMethodsAndTheAnglersSelection()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile());
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#profile-method-Fly").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#profile-method-Spinning").ClassList.Should().Contain("mud-chip-outlined");
            cut.Find("#profile-method-default-Fly").Should().NotBeNull();
            cut.Find("#profile-species-section-Fly").Should().NotBeNull();
            cut.Find("#profile-species-Fly-BrownTrout").Should().NotBeNull();
            cut.FindAll("#profile-species-section-Spinning").Should().BeEmpty();
        });
        await preferenceClient.Received(1).GetCatalogueAsync(Arg.Any<CancellationToken>());
        await preferenceClient.Received(1).GetPreferencesAsync(Arg.Any<CancellationToken>());
        await preferenceClient.DidNotReceive().UpdatePreferencesAsync(
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMakeTheFirstSelectedMethodTheDefault()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile());
        var preferenceClient = QuietFishingPreferenceClient(new FishingPreferencesDto([]), SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-method-Spinning"));

        // Act
        await cut.Find("#profile-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#profile-method-default-Spinning").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#profile-species-section-Spinning").Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ItShouldPromoteAnotherDefaultWhenTheDefaultMethodIsDeselected()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile());
        var preferences = new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(FlyMethodId, "Fly", "Fly", true, []),
            new FishingMethodPreferenceDto(SpinningMethodId, "Spinning", "Spinning", false, [])
        ]);
        var preferenceClient = QuietFishingPreferenceClient(preferences, SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-method-default-Fly"));

        // Act
        await cut.Find("#profile-method-Fly").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#profile-method-default-Fly").Should().BeEmpty();
            cut.Find("#profile-method-default-Spinning").ClassList.Should().Contain("mud-chip-filled");
        });
    }

    [Fact]
    public async Task ItShouldAddASpeciesChosenFromTheFullCatalogue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile());
        var preferences = new FishingPreferencesDto(
            [new FishingMethodPreferenceDto(FlyMethodId, "Fly", "Fly", true, [])]);
        var preferenceClient = QuietFishingPreferenceClient(preferences, SampleCatalogue());
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult(
                new CatalogueOptionModel(PikeSpeciesId, "Pike", "Pike")));
        await using var context = CreateContext(profileClient, preferenceClient, modalService);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-species-more-Fly"));

        // Act
        await cut.Find("#profile-species-more-Fly").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#profile-species-Fly-Pike").Should().NotBeNull();
            cut.Find("#profile-species-remove-Fly-Pike").Should().NotBeNull();
        });
        await modalService.Received(1)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Is<CataloguePickerModalModel>(model => model.Options.Count == 2),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemoveASpeciesFromAMethod()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile());
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-species-Fly-BrownTrout"));

        // Act
        await cut.Find("#profile-species-remove-Fly-BrownTrout").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-species-Fly-BrownTrout").Should().BeEmpty());
    }

    [Fact]
    public async Task ItShouldKeepTheStoredSpeciesListWhenTheAnglerHasNoChipSelection()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(new ProfileDto(
                userId,
                "Eamonn",
                null,
                null,
                null,
                "Westmeath",
                ["Coarse"],
                ["Pike", "Tench"],
                true,
                false,
                false,
                false,
                false));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        var preferenceClient = QuietFishingPreferenceClient(new FishingPreferencesDto([]), SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.PreferredSpecies.SequenceEqual(new[] { "Pike", "Tench" })
                && profile.PreferredFishingTypes.SequenceEqual(new[] { "Coarse" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepLegacySpeciesWhenAMethodIsSelectedWithNoSpeciesYet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = LegacyProfileClient(userId);
        var preferenceClient = QuietFishingPreferenceClient(new FishingPreferencesDto([]), SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-method-Fly"));

        // Act
        await cut.Find("#profile-method-Fly").ClickAsync();
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.PreferredSpecies.SequenceEqual(new[] { "Pike", "Tench" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepLegacySpeciesAlongsideNewlyChosenChipSpecies()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = LegacyProfileClient(userId);
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-species-Fly-BrownTrout"));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.PreferredSpecies.SequenceEqual(new[] { "Pike", "Tench", "Brown Trout" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepLegacySpeciesAfterAChipSpeciesIsRemoved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = LegacyProfileClient(userId);
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-species-Fly-BrownTrout"));

        // Act
        await cut.Find("#profile-species-remove-Fly-BrownTrout").ClickAsync();
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.PreferredSpecies.SequenceEqual(new[] { "Pike", "Tench" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTheChosenMeasurementUnits()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-weight-unit"));

        // Act
        await cut.InvokeAsync(() =>
            cut.FindComponent<MudBlazor.MudSelect<WeightUnitEnum>>().Instance.ValueChanged
                .InvokeAsync(WeightUnitEnum.Lb));
        await cut.InvokeAsync(() =>
            cut.FindComponent<MudBlazor.MudSelect<LengthUnitEnum>>().Instance.ValueChanged
                .InvokeAsync(LengthUnitEnum.In));
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Is<UpdateProfileDto>(profile =>
                profile.PreferredWeightUnit == WeightUnitEnum.Lb
                && profile.PreferredLengthUnit == LengthUnitEnum.In),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ItShouldNotBeAbleToRewriteStoredCatchMeasurements()
    {
        // Arrange
        // Act
        var injected = typeof(ProfilePage)
            .GetProperties(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            .Where(property =>
                property.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.InjectAttribute), true).Length > 0)
            .Select(property => property.PropertyType)
            .ToArray();

        // Assert
        injected.Should().Contain(typeof(IProfileClient));
        injected.Should().Contain(typeof(IFishingPreferenceClient));
        injected.Should().NotContain(typeof(FishingLogBook.Web.Features.Catch.Offline.ICatchStore));
        injected.Should().NotContain(typeof(FishingLogBook.Web.Features.Catch.Services.ICatchClient));
    }

    private static IProfileClient LegacyProfileClient(Guid userId)
    {
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>())
            .Returns(new ProfileDto(
                userId,
                "Eamonn",
                null,
                null,
                null,
                "Westmeath",
                ["Coarse"],
                ["Pike", "Tench"],
                true,
                false,
                false,
                true,
                true));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        return profileClient;
    }

    [Fact]
    public async Task ItShouldShowFrenchPreferenceCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile());
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(profileClient, preferenceClient);

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Méthodes de pêche préférées");
            cut.Markup.Should().Contain("Unité de poids préférée");
            cut.Markup.Should().Contain("Espèces préférées pour Fly");
            cut.Find("#profile-species-more-Fly").TextContent.Should().Contain("Plus…");
        });
    }
}
