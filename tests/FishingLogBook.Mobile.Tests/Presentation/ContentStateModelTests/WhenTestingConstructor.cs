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

    [Theory]
    [InlineData(ContentStateEnum.Ready, false)]
    [InlineData(ContentStateEnum.Loading, true)]
    [InlineData(ContentStateEnum.Empty, false)]
    [InlineData(ContentStateEnum.Error, false)]
    [InlineData(ContentStateEnum.Offline, false)]
    [InlineData(ContentStateEnum.Planned, false)]
    public void ItShouldShowTheLoaderOnlyForTheLoadingState(ContentStateEnum state, bool expected)
    {
        // Arrange
        // Act
        var sut = new ContentStateModel(state, _text);
        // Assert
        sut.IsLoading.Should().Be(expected);
    }

    [Fact]
    public void ItShouldNotOfferRetryOrTheEmptyActionWhileLoading()
    {
        // Arrange
        // Act
        var sut = new ContentStateModel(ContentStateEnum.Loading, _text);
        // Assert
        sut.IsVisible.Should().BeTrue();
        sut.CanRetry.Should().BeFalse();
        sut.IsEmpty.Should().BeFalse();
    }

    [Theory]
    [InlineData(ContentStateEnum.Error)]
    [InlineData(ContentStateEnum.Offline)]
    public void ItShouldOfferRetryWithoutTheLoaderWhenLoadingFailsOrTheDeviceIsOffline(ContentStateEnum state)
    {
        // Arrange
        // Act
        var sut = new ContentStateModel(state, _text);
        // Assert
        sut.CanRetry.Should().BeTrue();
        sut.IsLoading.Should().BeFalse();
        sut.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void ItShouldShowTheGenuineEmptyStateWithItsActionAndNoLoaderOrRetry()
    {
        // Arrange
        // Act
        var sut = new ContentStateModel(ContentStateEnum.Empty, _text);
        // Assert
        sut.IsEmpty.Should().BeTrue();
        sut.IsLoading.Should().BeFalse();
        sut.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void ItShouldHideTheWholeViewOnceTheContentIsReady()
    {
        // Arrange
        // Act
        var sut = new ContentStateModel(ContentStateEnum.Ready, _text);
        // Assert
        sut.IsVisible.Should().BeFalse();
        sut.IsLoading.Should().BeFalse();
    }
}
