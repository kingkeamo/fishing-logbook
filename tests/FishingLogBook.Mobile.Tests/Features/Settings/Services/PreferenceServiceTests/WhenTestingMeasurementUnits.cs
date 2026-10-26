using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.Services.PreferenceServiceTests;

public class WhenTestingMeasurementUnits : BasePreferenceServiceTest
{
    [Fact]
    public void ItShouldDefaultToMetricUnits()
    {
        // Arrange
        // Act
        var sut = CreateSut();
        // Assert
        sut.WeightUnit.Should().Be(WeightUnitEnum.Kg);
        sut.LengthUnit.Should().Be(LengthUnitEnum.Cm);
    }

    [Fact]
    public void ItShouldRestoreStoredImperialUnits()
    {
        // Arrange
        MockStore.Get(PreferenceService.WeightUnitKey, "Kg").Returns("Lb");
        MockStore.Get(PreferenceService.LengthUnitKey, "Cm").Returns("In");
        // Act
        var sut = CreateSut();
        // Assert
        sut.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        sut.LengthUnit.Should().Be(LengthUnitEnum.In);
    }

    [Fact]
    public void ItShouldFallBackToMetricForUnknownStoredValues()
    {
        // Arrange
        MockStore.Get(PreferenceService.WeightUnitKey, "Kg").Returns("Stone");
        MockStore.Get(PreferenceService.LengthUnitKey, "Cm").Returns("Furlong");
        // Act
        var sut = CreateSut();
        // Assert
        sut.WeightUnit.Should().Be(WeightUnitEnum.Kg);
        sut.LengthUnit.Should().Be(LengthUnitEnum.Cm);
    }

    [Fact]
    public void ItShouldPersistTheSelectedWeightUnitOnly()
    {
        // Arrange
        var sut = CreateSut();
        // Act
        sut.SetWeightUnit(WeightUnitEnum.Lb);
        // Assert
        sut.WeightUnit.Should().Be(WeightUnitEnum.Lb);
        sut.LengthUnit.Should().Be(LengthUnitEnum.Cm);
        MockStore.Received(1).Set(PreferenceService.WeightUnitKey, "Lb");
        MockStore.DidNotReceive().Set(PreferenceService.LengthUnitKey, Arg.Any<string>());
    }

    [Fact]
    public void ItShouldPersistTheSelectedLengthUnitOnly()
    {
        // Arrange
        var sut = CreateSut();
        // Act
        sut.SetLengthUnit(LengthUnitEnum.In);
        // Assert
        sut.LengthUnit.Should().Be(LengthUnitEnum.In);
        sut.WeightUnit.Should().Be(WeightUnitEnum.Kg);
        MockStore.Received(1).Set(PreferenceService.LengthUnitKey, "In");
        MockStore.DidNotReceive().Set(PreferenceService.WeightUnitKey, Arg.Any<string>());
    }

    [Fact]
    public void ItShouldNotPersistAnUnchangedUnit()
    {
        // Arrange
        var sut = CreateSut();
        // Act
        sut.SetWeightUnit(WeightUnitEnum.Kg);
        sut.SetLengthUnit(LengthUnitEnum.Cm);
        // Assert
        MockStore.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void ItShouldRejectUnknownUnitsWithoutPersistingThem()
    {
        // Arrange
        var sut = CreateSut();
        var weight = () => sut.SetWeightUnit((WeightUnitEnum)99);
        var length = () => sut.SetLengthUnit((LengthUnitEnum)99);
        // Act
        // Assert
        weight.Should().Throw<ArgumentOutOfRangeException>();
        length.Should().Throw<ArgumentOutOfRangeException>();
        MockStore.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }
}
