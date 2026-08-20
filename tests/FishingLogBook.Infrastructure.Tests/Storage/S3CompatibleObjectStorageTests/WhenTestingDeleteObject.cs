using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using FishingLogBook.Domain.Config;
using FishingLogBook.Infrastructure.Storage;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.Storage.S3CompatibleObjectStorageTests;

public class WhenTestingDeleteObject
{
    [Fact]
    public async Task ItShouldDeleteTheRequestedObjectFromTheConfiguredBucket()
    {
        // Arrange
        var client = Substitute.For<IAmazonS3>();
        var config = new ObjectStorageConfig
        {
            ServiceUrl = "https://storage.test",
            AccessKeyId = "access-key",
            SecretAccessKey = "secret-key",
            BucketName = "catch-photographs"
        };
        using var sut = new S3CompatibleObjectStorage(config, client);

        // Act
        await sut.DeleteObjectAsync("catches/user/catch/photo", CancellationToken.None);

        // Assert
        await client.Received(1).DeleteObjectAsync(
            Arg.Is<DeleteObjectRequest>(request =>
                request.BucketName == "catch-photographs"
                && request.Key == "catches/user/catch/photo"),
            Arg.Any<CancellationToken>());
    }
}
