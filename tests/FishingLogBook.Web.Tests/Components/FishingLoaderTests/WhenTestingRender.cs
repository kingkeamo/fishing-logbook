using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Components.FishingLoader;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Components.FishingLoaderTests;

public class WhenTestingRender : BaseFishingLoaderTest
{
    [Fact]
    public async Task ItShouldAnnounceLoadingAsAnEnglishStatusAndHideTheArtworkFromAssistiveTechnology()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<FishingLoader>();

        // Assert
        var root = cut.Find(".fishing-loader");
        root.GetAttribute("role").Should().Be("status");
        root.GetAttribute("aria-label").Should().Be("Loading");
        cut.Find("svg").GetAttribute("aria-hidden").Should().Be("true");
    }

    [Fact]
    public async Task ItShouldAnnounceLoadingInFrenchWhenTheUiCultureIsFrench()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<FishingLoader>();

        // Assert
        cut.Find(".fishing-loader").GetAttribute("aria-label").Should().Be("Chargement");
    }

    [Fact]
    public async Task ItShouldAnnounceTheSuppliedLabelInsteadOfTheDefault()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<FishingLoader>(parameters => parameters.Add(loader => loader.Label, "Loading your dashboard…"));

        // Assert
        cut.Find(".fishing-loader").GetAttribute("aria-label").Should().Be("Loading your dashboard…");
    }

    [Fact]
    public async Task ItShouldFallBackToTheDefaultLabelWhenTheSuppliedLabelIsBlank()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<FishingLoader>(parameters => parameters.Add(loader => loader.Label, "  "));

        // Assert
        cut.Find(".fishing-loader").GetAttribute("aria-label").Should().Be("Loading");
    }

    [Fact]
    public async Task ItShouldKeepTheCallerIdAndMergeTheCallerClassWithItsOwn()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<FishingLoader>(parameters => parameters
            .Add(loader => loader.Kind, LoaderKindEnum.LeapingFish)
            .Add(loader => loader.Class, "align-self-center")
            .AddUnmatched("id", "catch-list-loading"));

        // Assert
        var root = cut.Find("#catch-list-loading");
        root.ClassList.Should().Contain(["fishing-loader", "fishing-loader-leap", "align-self-center"]);
    }

    [Fact]
    public async Task ItShouldUseTheRegularSizeUnlessCompactIsRequested()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var regular = context.Render<FishingLoader>();
        var compact = context.Render<FishingLoader>(parameters => parameters.Add(loader => loader.Compact, true));

        // Assert
        regular.Find(".fishing-loader").ClassList.Should().NotContain("is-compact");
        compact.Find(".fishing-loader").ClassList.Should().Contain("is-compact");
    }

    [Fact]
    public async Task ItShouldShowExactlyOneOfTheThreeDesignsByDefault()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<FishingLoader>();

        // Assert
        var designs = new[] { "fishing-loader-leap", "fishing-loader-boat", "fishing-loader-bobber" };
        designs.Count(design => cut.Find(".fishing-loader").ClassList.Contains(design)).Should().Be(1);
        cut.FindAll(".jumper, .craft, .rig").Should().HaveCount(1);
    }

    [Fact]
    public async Task ItShouldShowDifferentDesignsAcrossAppearancesWhenNoDesignIsPinned()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var shown = Enumerable.Range(0, 120)
            .Select(_ => context.Render<FishingLoader>().Find(".fishing-loader").ClassList.Single(name => name is "fishing-loader-leap" or "fishing-loader-boat" or "fishing-loader-bobber"))
            .Distinct()
            .ToArray();

        // Assert
        shown.Should().BeEquivalentTo(["fishing-loader-leap", "fishing-loader-boat", "fishing-loader-bobber"]);
    }

    [Fact]
    public async Task ItShouldKeepTheSameDesignWhileItStaysOnScreen()
    {
        // Arrange
        await using var context = CreateContext();
        var cut = context.Render<FishingLoader>();
        var first = cut.Find(".fishing-loader").ClassList.Single(name => name is "fishing-loader-leap" or "fishing-loader-boat" or "fishing-loader-bobber");

        // Act
        for (var index = 0; index < 20; index++)
        {
            cut.Render(parameters => parameters.Add(loader => loader.Compact, index % 2 == 0));
        }

        // Assert
        cut.Find(".fishing-loader").ClassList.Should().Contain(first);
    }

    [Fact]
    public async Task ItShouldSwitchDesignWhenTheCallerChangesTheRequestedDesign()
    {
        // Arrange
        await using var context = CreateContext();
        var cut = context.Render<FishingLoader>(parameters => parameters.Add(loader => loader.Kind, LoaderKindEnum.Boat));

        // Act
        cut.Render(parameters => parameters.Add(loader => loader.Kind, LoaderKindEnum.Bobber));

        // Assert
        cut.Find(".fishing-loader").ClassList.Should().Contain("fishing-loader-bobber");
        cut.FindAll(".craft").Should().BeEmpty();
    }

    [Theory]
    [InlineData(LoaderKindEnum.LeapingFish, "fishing-loader-leap", ".jumper", ".craft", ".rig")]
    [InlineData(LoaderKindEnum.Boat, "fishing-loader-boat", ".craft", ".jumper", ".rig")]
    [InlineData(LoaderKindEnum.Bobber, "fishing-loader-bobber", ".rig", ".jumper", ".craft")]
    public async Task ItShouldRenderOnlyTheArtworkOfTheRequestedDesign(LoaderKindEnum kind, string expectedClass, string ownElement, string otherElement, string thirdElement)
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<FishingLoader>(parameters => parameters.Add(loader => loader.Kind, kind));

        // Assert
        cut.Find(".fishing-loader").ClassList.Should().Contain(expectedClass);
        cut.FindAll(ownElement).Should().HaveCount(1);
        cut.FindAll(otherElement).Should().BeEmpty();
        cut.FindAll(thirdElement).Should().BeEmpty();
    }

    [Fact]
    public void ItShouldStopEveryAnimationWhenReducedMotionIsRequested()
    {
        // Arrange
        var stylesheet = ReadComponentStylesheet();

        // Act
        var reducedMotion = stylesheet[stylesheet.IndexOf("@media (prefers-reduced-motion: reduce)", StringComparison.Ordinal)..];

        // Assert
        reducedMotion.Should().Contain("@media (prefers-reduced-motion: reduce)");
        foreach (var animated in new[] { ".fishing-loader-leap .water", ".fishing-loader-leap .splash", ".fishing-loader-leap .jumper", ".fishing-loader-boat .wake", ".fishing-loader-boat .craft", ".fishing-loader-boat .prop", ".fishing-loader-bobber .rig", ".fishing-loader-bobber .worm", ".fishing-loader-bobber .taker" })
        {
            reducedMotion.Should().Contain(animated);
        }

        reducedMotion.Should().Contain("animation: none");
    }

    [Fact]
    public void ItShouldTakeItsInkFromTheMudBlazorPaletteSoItAdaptsToTheDarkTheme()
    {
        // Arrange
        // Act
        var stylesheet = ReadComponentStylesheet();

        // Assert
        stylesheet.Should().Contain("--fishing-loader-ink: var(--mud-palette-text-primary)");
        stylesheet.Should().NotContainEquivalentOf("#0b1f3a");
    }
}
