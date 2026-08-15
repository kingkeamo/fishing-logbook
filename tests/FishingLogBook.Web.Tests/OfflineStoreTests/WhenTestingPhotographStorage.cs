using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.OfflineStoreTests;

public class WhenTestingPhotographStorage : BaseOfflineStoreTest
{
    [Fact]
    public void ItShouldStorePhotographBytesInsteadOfBlobs()
    {
        // Arrange
        var script = ReadOfflineStoreScript();

        // Act
        // Assert
        script.Should().Contain("store.put({ id, bytes: storedBytes, contentType })");
        script.Should().NotContain("new Blob(");
        script.Should().Contain("bytesBase64");
        script.Should().Contain("uint8ToBase64");
    }

    [Fact]
    public void ItShouldConvertStoredBytesBeforeTheDatabaseCloses()
    {
        // Arrange
        var script = ReadOfflineStoreScript();

        // Act
        // Assert
        script.Should().Contain("return await withTimeout(readPhotograph(db, id, started), openTimeoutMs, 'IndexedDB photograph read')");
        script.Should().Contain("item = photographFromRecord(request.result)");
        script.Should().Contain("bytesBase64: uint8ToBase64(toUint8Array(record.bytes))");
    }
}
