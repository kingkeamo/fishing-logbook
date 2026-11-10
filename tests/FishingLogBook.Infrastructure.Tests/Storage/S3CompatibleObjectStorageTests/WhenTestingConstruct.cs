using AwesomeAssertions;
using FishingLogBook.Domain.Config;
using FishingLogBook.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FishingLogBook.Infrastructure.Tests.Storage.S3CompatibleObjectStorageTests;

public class WhenTestingConstruct : BaseS3CompatibleObjectStorageTest
{
    [Fact]
    public async Task ItShouldBeUnconfiguredAndRefuseOperationsWhenSettingsAreMissing()
    {
        // Arrange
        using var sut = new S3CompatibleObjectStorage(
            Options.Create(new ObjectStorageConfig()),
            NullLogger<S3CompatibleObjectStorage>.Instance);

        // Act
        var act = () => sut.ExistsAsync("catches/catch/photo", CancellationToken.None);

        // Assert
        sut.IsConfigured.Should().BeFalse();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ItShouldPresignRealUrlsForTheConfiguredBucketAndKey()
    {
        // Arrange
        using var sut = new S3CompatibleObjectStorage(
            Options.Create(ConfiguredConfig()),
            NullLogger<S3CompatibleObjectStorage>.Instance);

        // Act
        var upload = await sut.CreateUploadUrlAsync(
            "catches/catch/photo",
            "image/jpeg",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
        var download = await sut.CreateDownloadUrlAsync(
            "catches/catch/photo",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        sut.IsConfigured.Should().BeTrue();
        upload.Host.Should().Be("storage.test");
        upload.AbsolutePath.Should().Be("/catch-photographs/catches/catch/photo");
        upload.Query.Should().Contain("X-Amz-Signature");
        download.AbsolutePath.Should().Be("/catch-photographs/catches/catch/photo");
        download.Query.Should().Contain("X-Amz-Signature");
        download.Should().NotBe(upload);
    }
}
