using AwesomeAssertions;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Shared.Tests.Constants.PhotographContentTypeConstantsTests;

public class WhenTestingIsAllowed
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("image/gif")]
    [InlineData("image/heic")]
    [InlineData("application/pdf")]
    public void ItShouldRejectMissingAndUnsupportedContentTypes(string? contentType)
    {
        // Arrange
        // Act
        var result = PhotographContentTypeConstants.IsAllowed(contentType);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("IMAGE/JPEG")]
    public void ItShouldAcceptEverySupportedContentTypeIgnoringCase(string contentType)
    {
        // Arrange
        // Act
        var result = PhotographContentTypeConstants.IsAllowed(contentType);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ItShouldListExactlyTheSupportedContentTypes()
    {
        // Arrange
        // Act
        var allowed = PhotographContentTypeConstants.Allowed;

        // Assert
        allowed.Should().BeEquivalentTo(["image/jpeg", "image/png", "image/webp"]);
    }
}
