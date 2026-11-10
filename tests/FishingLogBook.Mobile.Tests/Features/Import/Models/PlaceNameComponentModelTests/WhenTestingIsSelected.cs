using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Models;

namespace FishingLogBook.Mobile.Tests.Features.Import.Models.PlaceNameComponentModelTests;

public class WhenTestingIsSelected
{
    [Fact]
    public void ItShouldNotRaiseAChangeWhenTheValueIsUnchanged()
    {
        // Arrange
        var sut = new PlaceNameComponentModel("Dublin", true);
        var raised = new List<string?>();
        sut.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        // Act
        sut.IsSelected = true;

        // Assert
        raised.Should().BeEmpty();
        sut.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRaiseAChangeForIsSelectedWhenTheValueChanges()
    {
        // Arrange
        var sut = new PlaceNameComponentModel("Dublin", false);
        var raised = new List<string?>();
        sut.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        // Act
        sut.IsSelected = true;

        // Assert
        raised.Should().Equal(nameof(PlaceNameComponentModel.IsSelected));
        sut.IsSelected.Should().BeTrue();
        sut.Text.Should().Be("Dublin");
    }
}
