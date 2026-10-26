using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Measurements.Models;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Tests.Features.Measurements.ViewModels.MeasurementEditorViewModelTests;

public class WhenTestingMeasurementEditor
{
    private static MeasurementEditorViewModel Create(
        MeasurementKindEnum kind,
        decimal? canonical = null,
        bool imperial = false,
        string culture = "en-GB")
    {
        return new MeasurementEditorViewModel(
            new MeasurementService(),
            new UiTextService(CultureInfo.GetCultureInfo(culture)),
            kind,
            canonical,
            imperial ? WeightUnitEnum.Lb : WeightUnitEnum.Kg,
            imperial ? LengthUnitEnum.In : LengthUnitEnum.Cm);
    }

    [Theory]
    [InlineData(0.04, 0.0)]
    [InlineData(0.05, 0.1)]
    [InlineData(1.26, 1.3)]
    [InlineData(99, 5.0)]
    [InlineData(-3, 0.0)]
    public void ItShouldSnapMetricWeightSliderToTenthsWithinTheRange(double input, double expected)
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight);
        // Act
        sut.SetSliderValue((decimal)input);
        // Assert
        sut.SliderValue.Should().Be((decimal)expected);
    }

    [Fact]
    public void ItShouldSnapImperialWeightSliderToQuarterPounds()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        // Act
        sut.SetSliderValue(2.13m);
        // Assert
        sut.SliderValue.Should().Be(2.25m);
        sut.Pounds.Should().Be(2);
        sut.Ounces.Should().Be(4);
    }

    [Fact]
    public void ItShouldSnapLengthSliderToHalfInches()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length, imperial: true);
        // Act
        sut.SetSliderValue(10.3m);
        // Assert
        sut.SliderValue.Should().Be(10.5m);
        sut.CanonicalValue.Should().Be(26.67m);
    }

    [Fact]
    public void ItShouldRollOuncesIntoThePoundWhenIncreasingPastFifteen()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        sut.SetPounds(1);
        sut.SetOunces(15);
        // Act
        sut.Increase();
        // Assert
        sut.Pounds.Should().Be(2);
        sut.Ounces.Should().Be(0);
    }

    [Fact]
    public void ItShouldBorrowAPoundWhenDecreasingFromZeroOunces()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        sut.SetPounds(2);
        // Act
        sut.Decrease();
        // Assert
        sut.Pounds.Should().Be(1);
        sut.Ounces.Should().Be(15);
    }

    [Fact]
    public void ItShouldNotGoBelowZeroWhenDecreasingImperialWeight()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        // Act
        sut.Decrease();
        // Assert
        sut.Pounds.Should().Be(0);
        sut.Ounces.Should().Be(0);
    }

    [Fact]
    public void ItShouldClampOuncesToFifteen()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        // Act
        sut.SetOunces(40);
        // Assert
        sut.Ounces.Should().Be(15);
    }

    [Fact]
    public void ItShouldStepMetricByTheFineStep()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length, 20m);
        // Act
        sut.Increase();
        sut.Increase();
        sut.Decrease();
        // Assert
        sut.CanonicalValue.Should().Be(21m);
    }

    [Fact]
    public void ItShouldPreserveTheCanonicalValueWhenSwitchingUnitsWithoutEditing()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, 2.345m);
        // Act
        sut.SetUnits(WeightUnitEnum.Lb, LengthUnitEnum.In);
        sut.SetUnits(WeightUnitEnum.Kg, LengthUnitEnum.Cm);
        // Assert
        sut.CanonicalValue.Should().Be(2.345m);
    }

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("1,5", 1.5)]
    public void ItShouldParseExactTextUsingTheAppCulture(string text, double expected)
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, culture: text.Contains(',') ? "fr-FR" : "en-GB");
        // Act
        sut.SetExactText(text);
        // Assert
        sut.CanonicalValue.Should().Be((decimal)expected);
        sut.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRejectUnparseableExactTextAndKeepTheValue()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, 2m);
        // Act
        sut.SetExactText("abc");
        // Assert
        sut.IsValid.Should().BeFalse();
        sut.HasValidationMessage.Should().BeTrue();
        sut.CanonicalValue.Should().Be(2m);
        sut.Apply().Should().BeNull();
    }

    [Fact]
    public void ItShouldRejectZeroAsAnInvalidMeasurement()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length);
        // Act
        sut.SetExactText("0");
        // Assert
        sut.Apply().Should().BeNull();
        sut.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ItShouldExpandTheRangeWhenTheValueExceedsIt()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight);
        var initialMaximum = sut.SliderMaximum;
        // Act
        sut.SetExactText("30");
        // Assert
        sut.SliderMaximum.Should().BeGreaterThan(initialMaximum);
        sut.CanonicalValue.Should().Be(30m);
    }

    [Fact]
    public void ItShouldApplyAValidValueAndClearIt()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length, 45m);
        // Act
        var applied = sut.Apply();
        sut.Clear();
        // Assert
        applied.Should().Be(45m);
        sut.CanonicalValue.Should().BeNull();
        sut.ExactText.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldRaiseChangeNotificationsWhenTheValueChanges()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length);
        var raised = 0;
        sut.PropertyChanged += (_, _) => raised++;
        // Act
        sut.SetSliderValue(10m);
        // Assert
        raised.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ItShouldRoundTripImperialLengthWithinCanonicalPrecision()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length, imperial: true);
        // Act
        sut.SetExactText("12.25");
        // Assert
        sut.CanonicalValue.Should().Be(31.12m);
    }
}
