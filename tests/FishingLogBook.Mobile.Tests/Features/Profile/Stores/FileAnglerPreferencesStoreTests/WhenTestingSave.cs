using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Mobile.Tests.Support;

namespace FishingLogBook.Mobile.Tests.Features.Profile.Stores.FileAnglerPreferencesStoreTests;

public class WhenTestingSave : BaseFileAnglerPreferencesStoreTest
{
    [Fact]
    public async Task ItShouldRejectAnEmptyAccount()
    {
        // Arrange
        var action = () => Sut.SaveAsync(Guid.Empty, TestAnglerPreferences.Create(), CancellationToken.None);
        // Act
        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
        System.IO.Directory.Exists(Directory).Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldReplaceTheAccountsPreviousCacheWithoutLeavingTemporaryFiles()
    {
        // Arrange
        await Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None);
        var replacement = TestAnglerPreferences.Empty();
        // Act
        await Sut.SaveAsync(FirstUserId, replacement, CancellationToken.None);
        // Assert
        var result = await Sut.GetAsync(FirstUserId, CancellationToken.None);
        result.Should().BeEquivalentTo(replacement);
        System.IO.Directory.GetFiles(Directory).Should().Equal(PathFor(FirstUserId));
    }

    [Fact]
    public async Task ItShouldKeepEachAccountInItsOwnFile()
    {
        // Arrange
        var first = TestAnglerPreferences.Create();
        var second = TestAnglerPreferences.Empty();
        // Act
        await Sut.SaveAsync(FirstUserId, first, CancellationToken.None);
        await Sut.SaveAsync(SecondUserId, second, CancellationToken.None);
        // Assert
        (await Sut.GetAsync(FirstUserId, CancellationToken.None)).Should().BeEquivalentTo(first);
        (await Sut.GetAsync(SecondUserId, CancellationToken.None)).Should().BeEquivalentTo(second);
    }

    [Fact]
    public async Task ItShouldStoreOnlyPreferenceDataAndNoAuthenticationFields()
    {
        // Arrange
        // Act
        await Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None);
        // Assert
        var json = await File.ReadAllTextAsync(PathFor(FirstUserId));
        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("catalogue", "preferences", "locations");
        json.Should().NotContainEquivalentOf("token");
        json.Should().NotContainEquivalentOf("bearer");
    }
}
