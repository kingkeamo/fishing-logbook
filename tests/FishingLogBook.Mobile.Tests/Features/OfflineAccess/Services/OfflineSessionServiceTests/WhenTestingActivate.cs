using AwesomeAssertions;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Services.OfflineSessionServiceTests;

public sealed class WhenTestingActivate : BaseOfflineSessionServiceTest
{
    [Fact]
    public void ItShouldRejectANullCapability()
    {
        // Arrange

        // Act
        var action = () => Sut.Activate(null!);

        // Assert
        var exception = action.Should().Throw<ArgumentNullException>();
        exception.Which.ParamName.Should().Be("capability");
        Sut.Current.Should().BeNull();
    }

    [Fact]
    public void ItShouldReplaceTheActiveCapability()
    {
        // Arrange
        var replacement = new OfflineCapabilityModel(
            "replacement-subject",
            Guid.Parse("759be22d-4248-49c4-a9d2-7112d48329d1"),
            true);
        Sut.Activate(Capability);

        // Act
        Sut.Activate(replacement);

        // Assert
        Sut.Current.Should().BeSameAs(replacement);
    }

    [Fact]
    public void ItShouldExposeTheActiveCapability()
    {
        // Arrange

        // Act
        Sut.Activate(Capability);

        // Assert
        Sut.Current.Should().BeSameAs(Capability);
    }
}
