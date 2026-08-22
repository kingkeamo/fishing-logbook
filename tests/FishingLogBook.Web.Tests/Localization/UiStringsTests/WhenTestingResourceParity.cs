using System.Globalization;
using System.Resources;
using AwesomeAssertions;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Localization.UiStringsTests;

public class WhenTestingResourceParity
{
    [Fact]
    public void ItShouldTranslateEveryEnglishKeyIntoFrench()
    {
        // Arrange
        var manager = new ResourceManager(typeof(UiStrings));
        var english = manager.GetResourceSet(new CultureInfo(CultureNames.English), true, false);
        var french = manager.GetResourceSet(new CultureInfo(CultureNames.French), true, false);

        // Act
        var englishKeys = Keys(english);
        var frenchKeys = Keys(french);

        // Assert
        englishKeys.Should().NotBeEmpty();
        frenchKeys.Should().BeEquivalentTo(englishKeys);
    }

    [Fact]
    public void ItShouldTranslateTheNewApplicationShellStrings()
    {
        // Arrange
        var manager = new ResourceManager(typeof(UiStrings));
        var french = new CultureInfo(CultureNames.French);

        // Act
        var menu = manager.GetString("Auth_UserMenu", french);
        var avatar = manager.GetString("Auth_UserAvatarAlt", french);

        // Assert
        menu.Should().Be("Menu du compte");
        avatar.Should().Be("Votre photo de profil");
    }

    [Fact]
    public void ItShouldKeepInstallCopyFreeOfLegacyProductWording()
    {
        // Arrange
        var manager = new ResourceManager(typeof(UiStrings));
        var english = manager.GetResourceSet(new CultureInfo(CultureNames.English), true, false);
        var french = manager.GetResourceSet(new CultureInfo(CultureNames.French), true, false);

        // Act
        var installValues = Values(english)
            .Concat(Values(french))
            .ToArray();

        // Assert
        installValues.Should().NotBeEmpty();
        installValues.Should().AllSatisfy(value =>
        {
            value.Should().NotContain("Catch, But Don’t Forget");
            value.Should().NotContain("CBDF");
        });
    }

    [Fact]
    public void ItShouldUseTheCanonicalProductNameInUserFacingResources()
    {
        // Arrange
        var manager = new ResourceManager(typeof(UiStrings));
        var english = manager.GetResourceSet(new CultureInfo(CultureNames.English), true, false);
        var french = manager.GetResourceSet(new CultureInfo(CultureNames.French), true, false);

        // Act
        var appName = manager.GetString("App_Name", new CultureInfo(CultureNames.English));
        var userFacingValues = AllValues(english)
            .Concat(AllValues(french))
            .ToArray();

        // Assert
        appName.Should().Be("Catch But Don’t Forget");
        userFacingValues.Should().AllSatisfy(value =>
        {
            value.Should().NotContain("Catch, But Don’t Forget");
            value.Should().NotContain("CBDF");
        });
    }

    private static IReadOnlyCollection<string> Keys(ResourceSet? set)
    {
        return set is null
            ? []
            : [.. set.Cast<System.Collections.DictionaryEntry>().Select(entry => (string)entry.Key)];
    }

    private static IReadOnlyCollection<string> Values(ResourceSet? set)
    {
        return set is null
            ? []
            : [.. set.Cast<System.Collections.DictionaryEntry>()
                .Where(entry => ((string)entry.Key).StartsWith("Install_", StringComparison.Ordinal))
                .Select(entry => (string)entry.Value!)];
    }

    private static IReadOnlyCollection<string> AllValues(ResourceSet? set)
    {
        return set is null
            ? []
            : [.. set.Cast<System.Collections.DictionaryEntry>()
                .Select(entry => (string)entry.Value!)];
    }
}
