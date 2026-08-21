using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Localization.CatalogueResourcesTests;

public partial class WhenTestingCompleteness
{
    [Fact]
    public void ItShouldHaveEnglishAndFrenchLabelsForEverySeededCatalogueCode()
    {
        // Arrange
        var seed = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "FishingLogBook.Db.Migrations",
            "02_SeedData",
            "202608181902_87_SeedFishingCatalogue.sql"));
        var speciesStart = seed.IndexOf("INSERT INTO \"Species\"", StringComparison.Ordinal);
        var methodCodes = ExtractCodes(seed[..speciesStart]);
        var speciesCodes = ExtractCodes(seed[speciesStart..]);

        // Act
        var methodEnglish = Keys(new ResourceManager(typeof(FishingMethodStrings)), CultureInfo.InvariantCulture);
        var methodFrench = Keys(new ResourceManager(typeof(FishingMethodStrings)), new CultureInfo(CultureNames.French));
        var speciesEnglish = Keys(new ResourceManager(typeof(SpeciesStrings)), CultureInfo.InvariantCulture);
        var speciesFrench = Keys(new ResourceManager(typeof(SpeciesStrings)), new CultureInfo(CultureNames.French));

        // Assert
        methodEnglish.Should().Contain(methodCodes);
        methodFrench.Should().Contain(methodCodes);
        speciesEnglish.Should().Contain(speciesCodes);
        speciesFrench.Should().Contain(speciesCodes);
    }

    [Fact]
    public void ItShouldKeepCatalogueLabelsOutOfTheGeneralUiResources()
    {
        // Arrange
        var uiKeys = Keys(new ResourceManager(typeof(UiStrings)), CultureInfo.InvariantCulture);

        // Act
        var species = new ResourceManager(typeof(SpeciesStrings)).GetString("Pike", CultureInfo.InvariantCulture);
        var method = new ResourceManager(typeof(FishingMethodStrings)).GetString("Fly", CultureInfo.InvariantCulture);

        // Assert
        species.Should().Be("Pike");
        method.Should().Be("Fly");
        uiKeys.Should().NotContain(key => key.StartsWith("Catalogue_", StringComparison.Ordinal));
    }

    private static IReadOnlyCollection<string> ExtractCodes(string sql)
    {
        return [.. SeedRowRegex().Matches(sql).Select(match => match.Groups[1].Value)];
    }

    private static IReadOnlyCollection<string> Keys(ResourceManager manager, CultureInfo culture)
    {
        var resources = manager.GetResourceSet(culture, true, false);
        return resources is null
            ? []
            : [.. resources.Cast<System.Collections.DictionaryEntry>().Select(entry => (string)entry.Key)];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FishingLogBook.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    [GeneratedRegex(@"'[^']+'\s*,\s*'([^']+)'\s*,\s*'[^']+'", RegexOptions.CultureInvariant)]
    private static partial Regex SeedRowRegex();
}
