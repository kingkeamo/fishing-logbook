using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Tests.Localization.CatalogueLocalizerTests;

public class WhenTestingLocalize
{
    private static readonly Guid MethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid SpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    [Fact]
    public void ItShouldTranslateCatalogueNamesWithoutChangingIdentity()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        using var context = CreateContext();
        var subject = context.Services.GetRequiredService<ICatalogueLocalizer>();

        // Act
        var actual = subject.Localize(new FishingCatalogueDto(
            [new FishingMethodDto(MethodId, "Fly", "Fly")],
            [new SpeciesDto(SpeciesId, "BrownTrout", "Brown Trout")]));

        // Assert
        actual.Methods.Single().Should().Be(new FishingMethodDto(MethodId, "Fly", "Pêche à la mouche"));
        actual.AllSpecies.Single().Should().Be(new SpeciesDto(SpeciesId, "BrownTrout", "Truite brune"));
    }

    [Fact]
    public void ItShouldUseTheParentCultureBeforeTheEnglishFallback()
    {
        // Arrange
        using var culture = TestCulture.Use("fr-CA");
        using var context = CreateContext();
        var subject = context.Services.GetRequiredService<ICatalogueLocalizer>();

        // Act
        var actual = subject.Localize(new FishingCatalogueDto(
            [new FishingMethodDto(MethodId, "Fly", "Canonical fly")],
            []));

        // Assert
        actual.Methods.Single().Name.Should().Be("Pêche à la mouche");
    }

    [Fact]
    public void ItShouldUseTheCanonicalNameWhenNoResourceExists()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        using var context = CreateContext();
        var subject = context.Services.GetRequiredService<ICatalogueLocalizer>();

        // Act
        var actual = subject.Localize(new FishingCatalogueDto(
            [new FishingMethodDto(MethodId, "FutureMethod", "Future method")],
            []));

        // Assert
        actual.Methods.Single().Name.Should().Be("Future method");
    }

    [Fact]
    public void ItShouldFallBackToEnglishForAnUnsupportedCulture()
    {
        // Arrange
        using var culture = TestCulture.Use("de-DE");
        using var context = CreateContext();
        var subject = context.Services.GetRequiredService<ICatalogueLocalizer>();

        // Act
        var actual = subject.Localize(new FishingCatalogueDto(
            [new FishingMethodDto(MethodId, "Fly", "Canonical fly")],
            []));

        // Assert
        actual.Methods.Single().Name.Should().Be("Fly");
    }

    [Fact]
    public void ItShouldUseEnglishWhenTheFrenchResourceDoesNotContainTheKey()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<IStringLocalizer<CatalogueFallbackProbeStrings>>();

        // Act
        var actual = CatalogueLocalizer.Resolve(localizer, "EnglishOnly", "Canonical label");

        // Assert
        actual.Should().Be("English fallback");
    }

    [Fact]
    public void ItShouldUseTheFrenchParentResourceForFrenchFrance()
    {
        // Arrange
        using var culture = TestCulture.Use("fr-FR");
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<IStringLocalizer<CatalogueFallbackProbeStrings>>();

        // Act
        var actual = CatalogueLocalizer.Resolve(localizer, "ParentCulture", "Canonical label");

        // Assert
        actual.Should().Be("Valeur française parente");
    }

    [Fact]
    public void ItShouldUseTheCanonicalNameWhenNeitherFrenchNorEnglishContainsTheKey()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<IStringLocalizer<CatalogueFallbackProbeStrings>>();

        // Act
        var actual = CatalogueLocalizer.Resolve(localizer, "MissingEverywhere", "Canonical label");

        // Assert
        actual.Should().Be("Canonical label");
    }

    [Fact]
    public void ItShouldTranslateSavedPreferencesWithoutChangingTheirIdsOrDefaults()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        using var context = CreateContext();
        var subject = context.Services.GetRequiredService<ICatalogueLocalizer>();
        var preferences = new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(MethodId, "Fly", "Fly", true,
            [
                new FishingSpeciesPreferenceDto(SpeciesId, "BrownTrout", "Brown Trout", true)
            ])
        ]);

        // Act
        var actual = subject.Localize(preferences);

        // Assert
        actual.Methods.Single().FishingMethodId.Should().Be(MethodId);
        actual.Methods.Single().IsDefault.Should().BeTrue();
        actual.Methods.Single().Name.Should().Be("Pêche à la mouche");
        actual.Methods.Single().Species.Single().SpeciesId.Should().Be(SpeciesId);
        actual.Methods.Single().Species.Single().IsDefault.Should().BeTrue();
        actual.Methods.Single().Species.Single().Name.Should().Be("Truite brune");
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddLocalization();
        context.Services.AddScoped<ICatalogueLocalizer, CatalogueLocalizer>();
        return context;
    }
}
