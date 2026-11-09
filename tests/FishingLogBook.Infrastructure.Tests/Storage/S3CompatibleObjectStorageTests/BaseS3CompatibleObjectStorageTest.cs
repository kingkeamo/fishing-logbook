using Amazon.S3;
using FishingLogBook.Domain.Config;
using FishingLogBook.Infrastructure.Storage;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.Storage.S3CompatibleObjectStorageTests;

public class BaseS3CompatibleObjectStorageTest
{
    protected static ObjectStorageConfig ConfiguredConfig()
    {
        return new ObjectStorageConfig
        {
            ServiceUrl = "https://storage.test",
            AccessKeyId = "access-key",
            SecretAccessKey = "secret-key",
            BucketName = "catch-photographs"
        };
    }

    protected static (S3CompatibleObjectStorage Sut, IAmazonS3 Client) CreateSut()
    {
        var client = Substitute.For<IAmazonS3>();
        return (new S3CompatibleObjectStorage(ConfiguredConfig(), client), client);
    }
}
