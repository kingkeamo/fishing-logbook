using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using FishingLogBook.Domain.Config;
using FishingLogBook.Infrastructure.Storage;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.Storage.S3CompatibleObjectStorageTests;

public class WhenTestingExists
{
    [Fact]
    public async Task ItShouldReturnTrueWhenObjectMetadataExists()
    {
        var (sut, client) = CreateSut();
        using (sut)
        {
            client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetObjectMetadataResponse());

            var exists = await sut.ExistsAsync("catches/catch/photo", CancellationToken.None);

            exists.Should().BeTrue();
            await client.Received(1).GetObjectMetadataAsync(
                Arg.Is<GetObjectMetadataRequest>(request =>
                    request.BucketName == "catch-photographs" && request.Key == "catches/catch/photo"),
                Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task ItShouldReturnFalseWhenTheObjectDoesNotExist()
    {
        var (sut, client) = CreateSut();
        using (sut)
        {
            client.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<GetObjectMetadataResponse>(
                    new AmazonS3Exception("missing") { StatusCode = HttpStatusCode.NotFound }));

            var exists = await sut.ExistsAsync("catches/catch/photo", CancellationToken.None);

            exists.Should().BeFalse();
        }
    }

    private static (S3CompatibleObjectStorage Sut, IAmazonS3 Client) CreateSut()
    {
        var client = Substitute.For<IAmazonS3>();
        var config = new ObjectStorageConfig
        {
            ServiceUrl = "https://storage.test",
            AccessKeyId = "access-key",
            SecretAccessKey = "secret-key",
            BucketName = "catch-photographs"
        };
        return (new S3CompatibleObjectStorage(config, client), client);
    }
}
