using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Models.CatchFilterDraftModelTests;

public class WhenTestingDraft
{
    private readonly UiTextService _text = new(CultureInfo.GetCultureInfo("en-GB"));
    private readonly SpeciesOptionModel _pike = new(CatchSpeciesEnum.Pike, "Pike", 1, false);
    private readonly SpeciesOptionModel _salmon = new(CatchSpeciesEnum.AtlanticSalmon, "Atlantic salmon", 3, false);
    private readonly CatchFilterDraftModel _sut;

    public WhenTestingDraft()
    {
        _sut = new CatchFilterDraftModel(_text, CatchFilterModel.None, [_salmon, _pike]);
    }

    [Fact]
    public void ItShouldStartFromTheCurrentFilterWithoutActiveCount()
    {
        // Arrange
        // Act
        var filter = _sut.ToFilter();
        // Assert
        filter.Should().BeEquivalentTo(CatchFilterModel.None);
        _sut.ActiveCount.Should().Be(0);
        _sut.ApplyText.Should().Be("Apply filters");
        _sut.AllMethodsSelected.Should().BeTrue();
        _sut.AllTimeSelected.Should().BeTrue();
    }

    [Fact]
    public void ItShouldCountMethodSpeciesDateRangeAndLocationInTheApplyText()
    {
        // Arrange
        // Act
        _sut.SetMethod(CatchMethodEnum.Lure);
        _salmon.IsSelected = true;
        _pike.IsSelected = true;
        _sut.SetDateRange(CatchDateRangeEnum.Last30Days);
        _sut.SetLocation("  Corrib ");
        // Assert
        _sut.ActiveCount.Should().Be(5);
        _sut.ApplyText.Should().Be("Apply filters (5)");
        _sut.ToFilter().Location.Should().Be("Corrib");
        _sut.ToFilter().Species.Should().BeEquivalentTo([CatchSpeciesEnum.AtlanticSalmon, CatchSpeciesEnum.Pike]);
    }

    [Fact]
    public void ItShouldRaiseChangesForSelectionStateWhenAnOptionChanges()
    {
        // Arrange
        var changes = new List<string?>();
        _sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        // Act
        _pike.IsSelected = true;
        _sut.SetMethod(CatchMethodEnum.Fly);
        // Assert
        changes.Should().Contain(nameof(_sut.ApplyText)).And.Contain(nameof(_sut.FlySelected)).And.Contain(nameof(_sut.AllMethodsSelected));
        _sut.FlySelected.Should().BeTrue();
        _sut.AllMethodsSelected.Should().BeFalse();
    }

    [Fact]
    public void ItShouldClearEveryDraftFilter()
    {
        // Arrange
        _sut.SetMethod(CatchMethodEnum.Other);
        _pike.IsSelected = true;
        _sut.SetDateRange(CatchDateRangeEnum.Last7Days);
        _sut.SetLocation("Mask");
        // Act
        _sut.Clear();
        // Assert
        _sut.ToFilter().Should().BeEquivalentTo(CatchFilterModel.None);
        _pike.IsSelected.Should().BeFalse();
        _sut.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void ItShouldTreatNullLocationAsEmpty()
    {
        // Arrange
        _sut.SetLocation("Mask");
        // Act
        _sut.SetLocation(null);
        // Assert
        _sut.Location.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldRejectUnsupportedMethodsAndDateRanges()
    {
        // Arrange
        // Act
        var method = () => _sut.SetMethod((CatchMethodEnum)99);
        var range = () => _sut.SetDateRange((CatchDateRangeEnum)99);
        // Assert
        method.Should().Throw<ArgumentOutOfRangeException>();
        range.Should().Throw<ArgumentOutOfRangeException>();
        _sut.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void ItShouldLocaliseTheApplyTextWhenTheLanguageChanges()
    {
        // Arrange
        _pike.IsSelected = true;
        // Act
        _text.SetCulture("fr-FR");
        // Assert
        _sut.ApplyText.Should().Be("Appliquer les filtres (1)");
    }
}
