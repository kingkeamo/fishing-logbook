using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.PlaceNameModalViewModelTests;

public class WhenTestingPlaceNameModalViewModel
{
    private static readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));

    [Fact]
    public void ItShouldDropBlankAndDuplicateComponentsIgnoringCase()
    {
        // Arrange
        var model = new PlaceNameModalModel(["Dublin", " dublin ", "", "  ", "Ireland"], null);

        // Act
        var sut = new PlaceNameModalViewModel(Text, model);

        // Assert
        sut.Components.Select(component => component.Text).Should().Equal("Dublin", "Ireland");
    }

    [Fact]
    public void ItShouldSelectEveryComponentWhenThereIsNoCurrentPlaceName()
    {
        // Arrange
        var model = new PlaceNameModalModel(["Dublin", "Ireland"], null);

        // Act
        var sut = new PlaceNameModalViewModel(Text, model);

        // Assert
        sut.Components.Should().OnlyContain(component => component.IsSelected);
        sut.Preview.Should().Be("Dublin, Ireland");
        sut.PreviewText.Should().Be("Dublin, Ireland");
        sut.CanApply.Should().BeTrue();
    }

    [Fact]
    public void ItShouldSelectOnlyTheComponentsAlreadyInTheCurrentPlaceName()
    {
        // Arrange
        var model = new PlaceNameModalModel(["Street", "Dublin", "Ireland"], "dublin, Ireland, Elsewhere");

        // Act
        var sut = new PlaceNameModalViewModel(Text, model);

        // Assert
        sut.Components.Where(component => component.IsSelected).Select(component => component.Text)
            .Should().Equal("Dublin", "Ireland");
        sut.Preview.Should().Be("Dublin, Ireland");
    }

    [Fact]
    public void ItShouldShowThePlaceholderAndNoResultWhenNothingIsSelected()
    {
        // Arrange
        var model = new PlaceNameModalModel(["Dublin"], "Somewhere else");

        // Act
        var sut = new PlaceNameModalViewModel(Text, model);

        // Assert
        sut.Preview.Should().BeNull();
        sut.PreviewText.Should().Be("No place name");
        sut.ToResult().PlaceName.Should().BeNull();
        sut.CanApply.Should().BeTrue();
    }

    [Fact]
    public void ItShouldBlockApplyingAPlaceNameLongerThanTheLimitAndAllowTheLimitItself()
    {
        // Arrange
        var atLimit = new string('a', CatchDetailConstants.MaxPlaceNameLength);
        var overLimit = new string('a', CatchDetailConstants.MaxPlaceNameLength + 1);

        // Act
        var allowed = new PlaceNameModalViewModel(Text, new PlaceNameModalModel([atLimit], null));
        var blocked = new PlaceNameModalViewModel(Text, new PlaceNameModalModel([overLimit], null));

        // Assert
        allowed.IsTooLong.Should().BeFalse();
        allowed.CanApply.Should().BeTrue();
        blocked.IsTooLong.Should().BeTrue();
        blocked.CanApply.Should().BeFalse();
    }

    [Fact]
    public void ItShouldRaiseEveryDependentChangeAndRecalculateWhenAComponentIsToggled()
    {
        // Arrange
        var sut = new PlaceNameModalViewModel(Text, new PlaceNameModalModel(["Dublin", "Ireland"], null));
        var raised = new List<string?>();
        sut.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        // Act
        sut.Toggle(sut.Components[1]);

        // Assert
        raised.Should().Equal(
            nameof(PlaceNameModalViewModel.Preview),
            nameof(PlaceNameModalViewModel.PreviewText),
            nameof(PlaceNameModalViewModel.IsTooLong),
            nameof(PlaceNameModalViewModel.CanApply));
        sut.Preview.Should().Be("Dublin");
        sut.ToResult().PlaceName.Should().Be("Dublin");
    }

    [Fact]
    public void ItShouldReselectAComponentWhenToggledTwice()
    {
        // Arrange
        var sut = new PlaceNameModalViewModel(Text, new PlaceNameModalModel(["Dublin", "Ireland"], null));
        var ireland = sut.Components[1];

        // Act
        sut.Toggle(ireland);
        sut.Toggle(ireland);

        // Assert
        ireland.IsSelected.Should().BeTrue();
        sut.Preview.Should().Be("Dublin, Ireland");
    }
}
