using AwesomeAssertions;
using FishingLogBook.Application.Catches.Services;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchPhotographObjectKeyBuilderTests;

public class WhenTestingBuild
{
    [Fact]
    public void ItShouldReturnTheCanonicalCatchPhotographKey()
    {
        // Arrange
        var sut = new CatchPhotographObjectKeyBuilder();
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var objectKey = sut.Build(catchId, photographId);

        // Assert
        objectKey.Should().Be($"catch-photographs/{catchId:D}/{photographId:D}");
    }
}
