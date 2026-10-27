using System.Collections;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;

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
    public async Task ItShouldRemoveTheTemporaryFileAndKeepThePreviousCacheWhenTheWriteIsCancelled()
    {
        // Arrange
        var previous = TestAnglerPreferences.Empty();
        await Sut.SaveAsync(FirstUserId, previous, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var cancelling = TestAnglerPreferences.Create() with
        {
            Catalogue = new FishingCatalogueDto(new CancellingList<FishingMethodDto>(cancellation), [])
        };
        var action = () => Sut.SaveAsync(FirstUserId, cancelling, cancellation.Token);
        // Act
        await action.Should().ThrowAsync<OperationCanceledException>();
        // Assert
        cancellation.IsCancellationRequested.Should().BeTrue();
        System.IO.Directory.GetFiles(Directory).Should().Equal(PathFor(FirstUserId));
        (await Sut.GetAsync(FirstUserId, CancellationToken.None)).Should().BeEquivalentTo(previous);
    }

    [Fact]
    public async Task ItShouldRemoveTheTemporaryFileWhenTheCacheCannotBeReplaced()
    {
        // Arrange
        System.IO.Directory.CreateDirectory(PathFor(FirstUserId));
        var action = () => Sut.SaveAsync(FirstUserId, TestAnglerPreferences.Create(), CancellationToken.None);
        // Act
        await action.Should().ThrowAsync<Exception>();
        // Assert
        System.IO.Directory.GetFiles(Directory).Should().BeEmpty();
        System.IO.Directory.Exists(PathFor(FirstUserId)).Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldCompleteConcurrentSavesForTheSameAccountWithOneIntactCache()
    {
        // Arrange
        var full = TestAnglerPreferences.Create();
        var empty = TestAnglerPreferences.Empty();
        var saves = Enumerable.Range(0, 25)
            .Select(index => Task.Run(() => Sut.SaveAsync(FirstUserId, index % 2 == 0 ? full : empty, CancellationToken.None)));
        // Act
        await Task.WhenAll(saves);
        // Assert
        System.IO.Directory.GetFiles(Directory).Should().Equal(PathFor(FirstUserId));
        var result = await Sut.GetAsync(FirstUserId, CancellationToken.None);
        result.Should().Match<AnglerPreferencesModel>(saved =>
            saved.Preferences.Methods.Count == full.Preferences.Methods.Count ||
            saved.Preferences.Methods.Count == empty.Preferences.Methods.Count);
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

    private sealed class CancellingList<T>(CancellationTokenSource cancellation) : IReadOnlyList<T>
    {
        public int Count => 0;

        public T this[int index] => throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<T> GetEnumerator()
        {
            cancellation.Cancel();
            return Enumerable.Empty<T>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
