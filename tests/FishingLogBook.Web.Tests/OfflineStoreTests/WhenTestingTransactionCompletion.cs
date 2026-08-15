using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.OfflineStoreTests;

public class WhenTestingTransactionCompletion : BaseOfflineStoreTest
{
    [Fact]
    public void ItShouldResolveWritesOnlyOnTransactionComplete()
    {
        // Arrange
        var script = ReadOfflineStoreScript();

        // Act
        var completeIndex = script.IndexOf("transaction.oncomplete = () => {", StringComparison.Ordinal);
        var resolveIndex = script.IndexOf("resolve(result);", StringComparison.Ordinal);

        // Assert
        completeIndex.Should().BeGreaterThan(0);
        resolveIndex.Should().BeGreaterThan(completeIndex);
        script.Should().Contain("request.onerror = () => fail(request.error)");
        script.Should().NotContain("request.onerror = () => { };");
    }

    [Fact]
    public void ItShouldReadCatchesWithACursor()
    {
        // Arrange
        var script = ReadOfflineStoreScript();

        // Act
        // Assert
        script.Should().Contain("store.openCursor()");
        script.Should().NotContain("store.getAll()");
    }

    [Fact]
    public void ItShouldTimeOutIndexedDbOperations()
    {
        // Arrange
        var script = ReadOfflineStoreScript();

        // Act
        // Assert
        script.Should().Contain("function withTimeout");
        script.Should().Contain("IndexedDB open");
        script.Should().Contain("IndexedDB photograph read");
        script.Should().Contain("timed out");
    }
}
