using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.Storage.S3CompatibleObjectStorageTests;

public class WhenTestingCreateDownloadUrl : BaseS3CompatibleObjectStorageTest
{
    [Fact]
    public async Task ItShouldFailWhenObjectStorageIsNotConfigured()
    {
        // Arrange
        using var sut = new Infrastructure.Storage.S3CompatibleObjectStorage(
            new Domain.Config.ObjectStorageConfig(),
            null!);

        // Act
        var act = () => sut.CreateDownloadUrlAsync(
            "catches/catch/photo",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ItShouldPresignAGetForTheObjectWithTheLifetime()
    {
        // Arrange
        var (sut, client) = CreateSut();
        using var disposable = sut;
        client.GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>()).Returns("https://storage.test/download?sig=2");
        var before = DateTime.UtcNow;

        // Act
        var url = await sut.CreateDownloadUrlAsync(
            "catches/catch/photo",
            TimeSpan.FromMinutes(10),
            CancellationToken.None);

        // Assert
        url.Should().Be(new Uri("https://storage.test/download?sig=2"));
        client.Received(1).GetPreSignedURL(
            Arg.Is<GetPreSignedUrlRequest>(request =>
                request.BucketName == "catch-photographs"
                && request.Key == "catches/catch/photo"
                && request.Verb == HttpVerb.GET
                && request.Expires >= before.AddMinutes(10)
                && request.Expires <= DateTime.UtcNow.AddMinutes(10)));
    }
}
