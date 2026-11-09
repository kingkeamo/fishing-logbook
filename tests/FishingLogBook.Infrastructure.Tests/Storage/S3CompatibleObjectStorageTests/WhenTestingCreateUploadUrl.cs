using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Infrastructure.Tests.Storage.S3CompatibleObjectStorageTests;

public class WhenTestingCreateUploadUrl : BaseS3CompatibleObjectStorageTest
{
    [Fact]
    public async Task ItShouldFailWhenObjectStorageIsNotConfigured()
    {
        // Arrange
        using var sut = new Infrastructure.Storage.S3CompatibleObjectStorage(
            new Domain.Config.ObjectStorageConfig(),
            null!);

        // Act
        var act = () => sut.CreateUploadUrlAsync(
            "catches/catch/photo",
            "image/jpeg",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        sut.IsConfigured.Should().BeFalse();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ItShouldPresignAPutForTheObjectWithTheContentTypeAndLifetime()
    {
        // Arrange
        var (sut, client) = CreateSut();
        using var disposable = sut;
        client.GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>()).Returns("https://storage.test/upload?sig=1");
        var before = DateTime.UtcNow;

        // Act
        var url = await sut.CreateUploadUrlAsync(
            "catches/catch/photo",
            "image/png",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        url.Should().Be(new Uri("https://storage.test/upload?sig=1"));
        sut.IsConfigured.Should().BeTrue();
        client.Received(1).GetPreSignedURL(
            Arg.Is<GetPreSignedUrlRequest>(request =>
                request.BucketName == "catch-photographs"
                && request.Key == "catches/catch/photo"
                && request.Verb == HttpVerb.PUT
                && request.ContentType == "image/png"
                && request.Expires >= before.AddMinutes(5)
                && request.Expires <= DateTime.UtcNow.AddMinutes(5)));
    }
}
