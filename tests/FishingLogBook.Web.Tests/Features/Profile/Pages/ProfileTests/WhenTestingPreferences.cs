using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
            cut.Find("#profile-method-chips").QuerySelectorAll("input").Should().BeEmpty();
            cut.Find("#profile-species-section-Fly").QuerySelectorAll("input").Should().BeEmpty();
        });
        await preferenceClient.Received(1).GetCatalogueAsync(Arg.Any<CancellationToken>());
        await preferenceClient.Received(1).GetPreferencesAsync(Arg.Any<CancellationToken>());
        await preferenceClient.DidNotReceive().UpdatePreferencesAsync(
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSeparateTheDefaultLabelFromTheMethodAndSpeciesName()
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
            cut.Find("#profile-method-default-Fly").TextContent.Should().Be("FlyÂ Default");
            cut.Find("#profile-species-Fly-BrownTrout").TextContent.Should().Be("Brown TroutÂ Default");
        });
    }

    [Fact]
    public async Task ItShouldNotLabelANonDefaultChip()
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

        // Act
        var cut = context.Render<ProfilePage>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#profile-method-default-Spinning").TextContent.Should().Be("Spinning");
            cut.Find("#profile-method-default-Fly").TextContent.Should().Be("FlyÂ Default");
        });
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
            [
                new CatalogueOptionModel(BrownTroutSpeciesId, "BrownTrout", "Brown Trout"),
                new CatalogueOptionModel(PikeSpeciesId, "Pike", "Pike")
            ]));
        await using var context = CreateContext(profileClient, preferenceClient, modalService);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-species-more-Fly"));

        // Act
        await cut.Find("#profile-species-more-Fly").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#profile-species-Fly-Pike").Should().NotBeNull();
            cut.Find("#profile-species-Fly-BrownTrout").Should().NotBeNull();
            cut.Find("#profile-species-remove-Fly-Pike").Should().NotBeNull();
        });
        await modalService.Received(1)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Is<CataloguePickerModalModel>(model =>
                    model.Options.Count == 2
                    && model.AllowMultiple
                    && model.SelectedOptionIds != null
                    && model.SelectedOptionIds.Count == 0),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAddAMethodChosenFromTheFullCatalogue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile());
        var preferenceClient = QuietFishingPreferenceClient(new FishingPreferencesDto([]), SampleCatalogue());
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult(
                new CatalogueOptionModel(SpinningMethodId, "Spinning", "Spinning")));
        await using var context = CreateContext(profileClient, preferenceClient, modalService);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-method-more"));

        // Act
        await cut.Find("#profile-method-more").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#profile-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#profile-species-section-Spinning").Should().NotBeNull();
        });
        await modalService.Received(1)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
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
        injected.Should().NotContain(typeof(FishingLogBook.Web.Features.Catch.Offline.Stores.ICatchStore));
        injected.Should().NotContain(typeof(FishingLogBook.Web.Features.Catch.Clients.ICatchClient));
    }

    [Fact]
    public async Task ItShouldHandTheSavedPreferencesToTheProviderWhenSaving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        var anglerPreferences = Substitute.For<IAnglerPreferencesProvider>();
        await using var context = CreateContext(
            profileClient,
            preferenceClient,
            anglerPreferences: anglerPreferences);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await anglerPreferences.Received(1).SetAsync(
            userId,
            Arg.Is<AnglerPreferencesModel>(preferences =>
                preferences.Catalogue.Methods.Count == 2
                && preferences.Preferences.Methods.Count == 1
                && preferences.Preferences.Methods[0].IsDefault),
            Arg.Any<CancellationToken>());
        await anglerPreferences.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillSaveWhenRememberingThePreferencesFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var userId = Guid.NewGuid();
        var profileClient = Substitute.For<IProfileClient>();
        profileClient.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(EmptyProfile(userId));
        profileClient.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>())
            .Returns(call => ToSaved(userId, call.ArgAt<UpdateProfileDto>(0)));
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        var anglerPreferences = Substitute.For<IAnglerPreferencesProvider>();
        anglerPreferences.SetAsync(
                Arg.Any<Guid>(),
                Arg.Any<AnglerPreferencesModel>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("quota exceeded"));
        await using var context = CreateContext(
            profileClient,
            preferenceClient,
            anglerPreferences: anglerPreferences);
        var cut = context.Render<ProfilePage>();
        cut.WaitForAssertion(() => cut.Find("#profile-save-button"));

        // Act
        await cut.Find("#profile-save-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("#profile-save-failed").Should().BeEmpty());
        await profileClient.Received(1).UpdateOwnAsync(
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<CancellationToken>());
        await anglerPreferences.Received(1).SetAsync(
            userId,
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
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
            cut.Markup.Should().Contain("MÃ©thodes de pÃªche prÃ©fÃ©rÃ©es");
            cut.Markup.Should().Contain("UnitÃ© de poids prÃ©fÃ©rÃ©e");
            cut.Markup.Should().Contain("EspÃ¨ces prÃ©fÃ©rÃ©es pour Fly");
            cut.Find("#profile-species-more-Fly").TextContent.Should().Contain("Plusâ€¦");
        });
    }
}

