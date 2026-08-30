using AwesomeAssertions;
using FishingLogBook.Application.Profiles.Builders;

namespace FishingLogBook.Application.Tests.Profiles.Builders.ProfilePhotographObjectKeyBuilderTests;

public class WhenTestingBuild
{
    [Fact]
    public void ItShouldReturnTheCanonicalProfilePhotographKey()
    {
        // Arrange
        var sut = new ProfilePhotographObjectKeyBuilder();
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var objectKey = sut.Build(userId, photographId);

        // Assert
        objectKey.Should().Be($"profiles/{userId:D}/{photographId:D}");
    }
}
