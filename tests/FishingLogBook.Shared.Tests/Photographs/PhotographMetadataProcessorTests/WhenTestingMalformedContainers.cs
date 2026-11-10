using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Photographs.PhotographMetadataProcessorTests;

public class WhenTestingMalformedContainers : BasePhotographMetadataProcessorTest
{
    private static readonly byte[] PngSignatureBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void ItShouldRefuseAPngWhoseChunkLengthRunsPastTheEndOfTheFile()
    {
        // Arrange
        var sut = CreateProcessor(LocalTime(TimeSpan.Zero));
        byte[] bytes =
        [
            .. PngSignatureBytes,
            0x00, 0x00, 0x10, 0x00,
            (byte)'I', (byte)'H', (byte)'D', (byte)'R',
            0x01, 0x02
        ];

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Png);

        // Assert
        sanitised.Should().BeNull();
    }

    [Fact]
    public void ItShouldRefuseAPngWithNoChunksAfterTheSignature()
    {
        // Arrange
        var sut = CreateProcessor(LocalTime(TimeSpan.Zero));

        // Act
        var sanitised = sut.Sanitise(PngSignatureBytes, PhotographContentTypeConstants.Png);

        // Assert
        sanitised.Should().BeNull();
    }

    [Fact]
    public void ItShouldRefuseAFileThatDoesNotStartWithThePngSignature()
    {
        // Arrange
        var sut = CreateProcessor(LocalTime(TimeSpan.Zero));
        var bytes = new byte[32];

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Png);

        // Assert
        sanitised.Should().BeNull();
    }

    [Fact]
    public void ItShouldRefuseAWebpThatIsTooShortToHoldAChunk()
    {
        // Arrange
        var sut = CreateProcessor(LocalTime(TimeSpan.Zero));
        byte[] bytes = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P'];

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Webp);

        // Assert
        sanitised.Should().BeNull();
    }

    [Fact]
    public void ItShouldRefuseAWebpWhoseChunkSizeRunsPastTheEndOfTheFile()
    {
        // Arrange
        var sut = CreateProcessor(LocalTime(TimeSpan.Zero));
        byte[] bytes =
        [
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0x20, 0x00, 0x00, 0x00,
            (byte)'W', (byte)'E', (byte)'B', (byte)'P',
            (byte)'V', (byte)'P', (byte)'8', (byte)' ', 0xFF, 0x00, 0x00, 0x00,
            0x01, 0x02
        ];

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Webp);

        // Assert
        sanitised.Should().BeNull();
    }

    [Fact]
    public void ItShouldRefuseAFileThatIsNotARiffWebp()
    {
        // Arrange
        var sut = CreateProcessor(LocalTime(TimeSpan.Zero));
        var bytes = new byte[32];

        // Act
        var sanitised = sut.Sanitise(bytes, PhotographContentTypeConstants.Webp);

        // Assert
        sanitised.Should().BeNull();
    }

    [Fact]
    public void ItShouldRefuseAnUnsupportedContentType()
    {
        // Arrange
        var sut = CreateProcessor(LocalTime(TimeSpan.Zero));

        // Act
        var sanitised = sut.Sanitise([1, 2, 3], "image/gif");

        // Assert
        sanitised.Should().BeNull();
    }
}
