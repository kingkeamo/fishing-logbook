using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Measurements.Models;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Tests.Features.Measurements.ViewModels.MeasurementEditorViewModelTests;

public sealed class WhenTestingPendingInputAndApply
{
    private static MeasurementEditorViewModel Create(
        MeasurementKindEnum kind,
        decimal? canonical = null,
        bool imperial = false)
    {
        return new MeasurementEditorViewModel(
            new MeasurementService(),
            new UiTextService(CultureInfo.GetCultureInfo("en-GB")),
            kind,
            canonical,
            imperial ? WeightUnitEnum.Lb : WeightUnitEnum.Kg,
            imperial ? LengthUnitEnum.In : LengthUnitEnum.Cm);
    }

    [Fact]
    public void ItShouldIncreaseFromPendingExactTextThenUpdateDisplay()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length);

        // Act
        sut.Increase(pendingExactText: "10");

        // Assert
        sut.ExactText.Should().Be("11");
        sut.CanonicalValue.Should().Be(11m);
    }

    [Fact]
    public void ItShouldDecreaseFromPendingExactTextThenUpdateDisplay()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length);

        // Act
        sut.Decrease(pendingExactText: "10");

        // Assert
        sut.ExactText.Should().Be("9");
        sut.CanonicalValue.Should().Be(9m);
    }

    [Fact]
    public void ItShouldIncreaseImperialOuncesFromPendingEntries()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);

        // Act
        sut.Increase(pendingPoundsText: "1", pendingOuncesText: "15");

        // Assert
        sut.Pounds.Should().Be(2);
        sut.Ounces.Should().Be(0);
        sut.DisplayText.Should().Be("2 lb 0 oz");
    }

    [Fact]
    public void ItShouldStepImperialOuncesRepeatedlyWithoutReReadingStalePendingText()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        sut.Increase(pendingPoundsText: "1", pendingOuncesText: "0");

        // Act
        sut.Increase();
        sut.Increase();

        // Assert
        sut.Pounds.Should().Be(1);
        sut.Ounces.Should().Be(3);
        sut.DisplayText.Should().Be("1 lb 3 oz");
    }

    [Fact]
    public void ItShouldLetTheSliderWinOverPendingEntryText()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);

        // Act
        sut.CommitPending(pendingPoundsText: "1", pendingOuncesText: "0");
        sut.SetSliderValue(2.25m);

        // Assert
        sut.Pounds.Should().Be(2);
        sut.Ounces.Should().Be(4);
        sut.DisplayText.Should().Be("2 lb 4 oz");
    }

    [Fact]
    public void ItShouldNotLetTryApplyCommitStaleEntryTextAfterTheSliderOwnsTheValue()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        sut.CommitPending(pendingPoundsText: "7", pendingOuncesText: "0");
        sut.SetSliderValue(2.25m);

        // Act
        sut.CommitInputUnlessSuperseded(pendingPoundsText: "7", pendingOuncesText: "0");
        var result = sut.TryApply(pendingPoundsText: "7", pendingOuncesText: "0");

        // Assert
        result.Succeeded.Should().BeTrue();
        sut.Pounds.Should().Be(2);
        sut.Ounces.Should().Be(4);
        sut.DisplayText.Should().Be("2 lb 4 oz");
        result.CanonicalValue.Should().BeApproximately(1.021m, 0.01m);
    }

    [Fact]
    public void ItShouldApplyTypedEntryTextAfterTheSliderWhenTheUserEditsAgain()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length);
        sut.SetExactText("7");
        sut.SetSliderValue(12m);
        sut.MarkEntryEdited();

        // Act
        var result = sut.TryApply(pendingExactText: "18");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.CanonicalValue.Should().Be(18m);
        sut.ExactText.Should().Be("18");
    }

    [Fact]
    public void ItShouldIgnoreFocusLossAfterTheSliderOwnsTheValue()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        sut.SetSliderValue(2.25m);

        // Act
        sut.CommitInputUnlessSuperseded(pendingPoundsText: "1", pendingOuncesText: "0");

        // Assert
        sut.Pounds.Should().Be(2);
        sut.Ounces.Should().Be(4);
        sut.DisplayText.Should().Be("2 lb 4 oz");
    }

    [Fact]
    public void ItShouldIgnoreFocusLossAfterPlusUpdatesTheValue()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length);
        sut.Increase(pendingExactText: "10");

        // Act
        sut.CommitInputUnlessSuperseded(pendingExactText: "10");

        // Assert
        sut.ExactText.Should().Be("11");
        sut.CanonicalValue.Should().Be(11m);
    }

    [Fact]
    public void ItShouldApplyPendingExactTextAndSucceed()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length);

        // Act
        var result = sut.TryApply(pendingExactText: "18");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.CanonicalValue.Should().Be(18m);
        result.ErrorMessage.Should().BeNull();
        sut.ExactText.Should().Be("18");
    }

    [Fact]
    public void ItShouldReturnInvalidWhenPendingExactTextIsUnparseable()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length, canonical: 12m);

        // Act
        var result = sut.TryApply(pendingExactText: "nope");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        sut.CanonicalValue.Should().Be(12m);
    }

    [Fact]
    public void ItShouldTreatEmptyPendingExactTextAsClearThenApply()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length, canonical: 40m);

        // Act
        var result = sut.TryApply(pendingExactText: string.Empty);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.CanonicalValue.Should().BeNull();
        sut.ExactText.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldApplyPendingImperialPoundsAndOunces()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);

        // Act
        var result = sut.TryApply(pendingPoundsText: "3", pendingOuncesText: "8");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.CanonicalValue.Should().BeApproximately(1.587m, 0.01m);
        sut.Pounds.Should().Be(3);
        sut.Ounces.Should().Be(8);
    }

    [Fact]
    public void ItShouldApplyZeroImperialEntriesAsClearedWeight()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        sut.TryApply(pendingPoundsText: "2", pendingOuncesText: "0").Succeeded.Should().BeTrue();

        // Act
        var result = sut.TryApply(pendingPoundsText: "0", pendingOuncesText: "0");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.CanonicalValue.Should().BeNull();
        sut.Pounds.Should().Be(0);
        sut.Ounces.Should().Be(0);
    }

    [Fact]
    public void ItShouldRejectZeroLengthFromPendingExactText()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length);

        // Act
        var result = sut.TryApply(pendingExactText: "0");

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ItShouldClearImperialPoundsAndOunces()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        sut.TryApply(pendingPoundsText: "4", pendingOuncesText: "2").Succeeded.Should().BeTrue();

        // Act
        sut.Clear();

        // Assert
        sut.CanonicalValue.Should().BeNull();
        sut.Pounds.Should().Be(0);
        sut.Ounces.Should().Be(0);
        sut.IsValid.Should().BeTrue();
    }
}
