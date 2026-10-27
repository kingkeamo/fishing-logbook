using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Tests.Presentation.ContentStateModelTests;

public class WhenTestingConstructor
{
    private readonly UiTextService _text = new(CultureInfo.GetCultureInfo("en-GB"));

    [Fact]
    public void ItShouldUseTheFeatureTextWhenATextKeyIsSupplied()
    {
        // Arrange
        // Act
        var sut = new ContentStateModel(ContentStateEnum.Offline, _text, "PreferencesOffline");
        // Assert
        sut.Title.Should().Be(_text["PreferencesOfflineTitle"]);
        sut.Message.Should().Be(_text["PreferencesOfflineBody"]);
        sut.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void ItShouldKeepTheExistingStateTextWhenNoTextKeyIsSupplied()
    {
        // Arrange
        // Act
        var sut = new ContentStateModel(ContentStateEnum.Offline, _text);
        // Assert
        sut.Title.Should().Be(_text["OfflineTitle"]);
        sut.Message.Should().Be(_text["OfflineBody"]);
    }
}
