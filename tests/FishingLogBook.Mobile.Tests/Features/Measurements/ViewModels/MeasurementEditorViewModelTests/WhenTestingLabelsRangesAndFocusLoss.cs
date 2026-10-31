using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Measurements.Models;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Measurements.ViewModels.MeasurementEditorViewModelTests;

public sealed class WhenTestingLabelsRangesAndFocusLoss
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

    [Fact]
    public void ItShouldExposeMetricWeightLabelsAndSteps()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight);

        // Act
        var title = sut.Title;
        var exact = sut.ExactLabel;
        var step = sut.FineStepText;

        // Assert
        title.Should().Be(sut.Text["MeasureWeight"]);
        sut.WeightUnit.Should().Be(WeightUnitEnum.Kg);
        sut.LengthUnit.Should().Be(LengthUnitEnum.Cm);
        exact.Should().Contain(sut.Text["MeasureUnitKg"]);
        step.Should().Contain("0.1");
        sut.SliderHint.Should().Be(sut.Text["MeasureSlide"]);
        sut.SliderDescription.Should().Contain(title);
        sut.CurrentDescription.Should().Contain(sut.Text["MeasureNotRecorded"]);
        sut.RangeGroupLabel.Should().Be(sut.Text["MeasureRangeGroup"]);
        sut.RangeHint.Should().Be(sut.Text["MeasureRangeHint"]);
        sut.IncreaseLabel.Should().Contain(step);
        sut.DecreaseLabel.Should().Contain(step);
        sut.IsSingleValue.Should().BeTrue();
        sut.SliderMinimumLabel.Should().Contain(sut.Text["MeasureUnitKg"]);
        sut.SliderMaximumLabel.Should().NotBeNullOrWhiteSpace();
        sut.FractionValue.Should().Be((double)sut.Fraction);
    }

    [Fact]
    public void ItShouldExposeImperialWeightFieldAndStepLabels()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);

        // Act / Assert
        sut.PoundsLabel.Should().Be(sut.Text["MeasurePounds"]);
        sut.OuncesLabel.Should().Be(sut.Text["MeasureOunces"]);
        sut.FineStepText.Should().Be(sut.Text["MeasureOneOunce"]);
        sut.IsImperialWeight.Should().BeTrue();
        sut.IsSingleValue.Should().BeFalse();
        sut.UnitLabel.Should().Be(sut.Text["MeasureUnitLb"]);
        sut.RangeLabel(sut.Ranges[0]).Should().Be(sut.Text["MeasureRange" + sut.Ranges[0].Range]);
    }

    [Fact]
    public void ItShouldExposeLengthLabelsInFrench()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length, culture: "fr-FR");

        // Act / Assert
        sut.Title.Should().Be(sut.Text["MeasureLength"]);
        sut.ExactLabel.Should().Contain(sut.Text["MeasureUnitCm"]);
        sut.UnitLabel.Should().Be(sut.Text["MeasureUnitCm"]);
        sut.DisplayText.Should().Be(sut.Text["MeasureNotRecorded"]);
        sut.SliderMinimumLabel.Should().Contain(sut.Text["MeasureUnitCm"]);
        Create(MeasurementKindEnum.Length, 12m, culture: "fr-FR").DisplayText.Should().Contain("12");
        var imperial = Create(MeasurementKindEnum.Length, imperial: true);
        imperial.UnitLabel.Should().Be(imperial.Text["MeasureUnitIn"]);
        MeasurementEditorViewModel.FineStepFor(MeasurementKindEnum.Length, WeightUnitEnum.Kg, LengthUnitEnum.In).Should().Be(0.25m);
        MeasurementEditorViewModel.SliderStepFor(MeasurementKindEnum.Length, WeightUnitEnum.Kg, LengthUnitEnum.In).Should().Be(0.5m);
    }

    [Fact]
    public void ItShouldSelectAnEnabledRangeAndIgnoreOneThatCannotDisplayTheValue()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, 1m);
        var medium = sut.Ranges.Single(range => range.Range == MeasurementScaleRangeEnum.Medium);
        var small = sut.Ranges.Single(range => range.Range == MeasurementScaleRangeEnum.Small);
        sut.SetExactText("10");

        // Act
        sut.SelectRange(small);
        var afterUnavailable = sut.SelectedRange.Range;
        sut.SelectRange(medium);

        // Assert
        afterUnavailable.Should().NotBe(MeasurementScaleRangeEnum.Small);
        sut.SelectedRange.Range.Should().Be(MeasurementScaleRangeEnum.Medium);
        sut.RangeDescription(sut.SelectedRange).Should().Contain(sut.Text["MeasureRangeMedium"]);
    }

    [Fact]
    public void ItShouldCommitPendingTextOnFocusLossWhenTheSliderDoesNotOwnTheValue()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length, 10m);

        // Act
        sut.CommitInputUnlessSuperseded(pendingExactText: "18");

        // Assert
        sut.ExactText.Should().Be("18");
        sut.CanonicalValue.Should().Be(18m);
    }

    [Theory]
    [InlineData(MeasurementKindEnum.Weight, WeightUnitEnum.Kg, LengthUnitEnum.Cm, 0.1, 0.1)]
    [InlineData(MeasurementKindEnum.Weight, WeightUnitEnum.Lb, LengthUnitEnum.In, 0.25, 0.0625)]
    [InlineData(MeasurementKindEnum.Length, WeightUnitEnum.Kg, LengthUnitEnum.Cm, 1, 1)]
    [InlineData(MeasurementKindEnum.Length, WeightUnitEnum.Kg, LengthUnitEnum.In, 0.5, 0.25)]
    public void ItShouldExposeSliderAndFineStepsForEachUnit(
        MeasurementKindEnum kind,
        WeightUnitEnum weightUnit,
        LengthUnitEnum lengthUnit,
        decimal sliderStep,
        decimal fineStep)
    {
        // Arrange
        var sut = new MeasurementEditorViewModel(
            new MeasurementService(),
            new UiTextService(CultureInfo.GetCultureInfo("en-GB")),
            kind,
            null,
            weightUnit,
            lengthUnit);

        // Act / Assert
        sut.SliderStep.Should().Be(sliderStep);
        sut.FineStep.Should().Be(fineStep);
        MeasurementEditorViewModel.SliderStepFor(kind, weightUnit, lengthUnit).Should().Be(sliderStep);
        MeasurementEditorViewModel.FineStepFor(kind, weightUnit, lengthUnit).Should().Be(fineStep);
    }

    [Fact]
    public void ItShouldDescribeAnUnavailableRangeInFrench()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, 10m, culture: "fr-FR");
        var small = sut.Ranges.Single(range => range.Range == MeasurementScaleRangeEnum.Small);

        // Act
        var description = sut.RangeDescription(small);

        // Assert
        description.Should().Be(string.Format(sut.Text.Culture, sut.Text["MeasureRangeUnavailable"], sut.RangeLabel(small)));
        sut.RangeLabel(small).Should().Be(sut.Text["MeasureRangeSmall"]);
    }

    [Fact]
    public void ItShouldIncreaseFromAnEmptyExactValue()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Length);

        // Act
        sut.Increase();

        // Assert
        sut.CanonicalValue.Should().Be(1m);
        sut.ExactText.Should().Be("1");
    }

    [Fact]
    public void ItShouldClearWhenExactTextIsNull()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, 2m);

        // Act
        sut.SetExactText(null);

        // Assert
        sut.CanonicalValue.Should().BeNull();
        sut.ExactText.Should().BeEmpty();
        sut.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ItShouldClampNegativePoundsToZero()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);
        sut.SetPounds(3);
        sut.SetOunces(4);

        // Act
        sut.SetPounds(-2);

        // Assert
        sut.Pounds.Should().Be(0);
        sut.Ounces.Should().Be(4);
    }

    [Fact]
    public void ItShouldDecreaseImperialWeightFromPendingEntries()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, imperial: true);

        // Act
        sut.Decrease(pendingPoundsText: "2", pendingOuncesText: "0");

        // Assert
        sut.Pounds.Should().Be(1);
        sut.Ounces.Should().Be(15);
    }

    [Fact]
    public void ItShouldIgnoreUnparseablePendingImperialEntries()
    {
        // Arrange
        var sut = Create(MeasurementKindEnum.Weight, 1m, imperial: true);
        var pounds = sut.Pounds;
        var ounces = sut.Ounces;

        // Act
        sut.CommitPending(pendingPoundsText: "x", pendingOuncesText: "y");

        // Assert
        sut.Pounds.Should().Be(pounds);
        sut.Ounces.Should().Be(ounces);
    }

    [Fact]
    public void ItShouldRefreshValidLabelsWhenCultureChanges()
    {
        // Arrange
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        var sut = new MeasurementEditorViewModel(
            new MeasurementService(),
            text,
            MeasurementKindEnum.Weight,
            2m,
            WeightUnitEnum.Kg,
            LengthUnitEnum.Cm);

        // Act
        text.SetCulture("fr-FR");

        // Assert
        sut.Title.Should().Be(text["MeasureWeight"]);
        sut.UnitLabel.Should().Be(text["MeasureUnitKg"]);
        sut.HasValidationMessage.Should().BeFalse();
    }

    [Fact]
    public void ItShouldUseCanonicalSliderBoundsWhenDisplayConversionReturnsNothing()
    {
        // Arrange
        var measurement = Substitute.For<IMeasurementService>();
        measurement.ToDisplayWeight(Arg.Any<decimal?>(), Arg.Any<WeightUnitEnum>()).Returns((decimal?)null);
        measurement.ToDisplayLength(Arg.Any<decimal?>(), Arg.Any<LengthUnitEnum>()).Returns((decimal?)null);
        measurement.FormatWeight(Arg.Any<decimal?>(), Arg.Any<WeightUnitEnum>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("formatted");
        var sut = new MeasurementEditorViewModel(
            measurement,
            new UiTextService(CultureInfo.GetCultureInfo("en-GB")),
            MeasurementKindEnum.Weight,
            2m,
            WeightUnitEnum.Kg,
            LengthUnitEnum.Cm);

        // Act
        var maximum = sut.SliderMaximum;
        var label = sut.SliderMinimumLabel;

        // Assert
        maximum.Should().Be(sut.SelectedRange.MaximumCanonical);
        label.Should().Be("formatted");
        measurement.Received(1).ToDisplayWeight(
            Arg.Is<decimal?>(value => value == sut.SelectedRange.MaximumCanonical),
            WeightUnitEnum.Kg);
        measurement.Received(1).FormatWeight(
            0m,
            WeightUnitEnum.Kg,
            sut.Text["MeasureUnitKg"],
            sut.Text["MeasureUnitOz"]);
    }

    [Fact]
    public void ItShouldRefreshInvalidMessageWhenCultureChanges()
    {
        // Arrange
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        var sut = new MeasurementEditorViewModel(
            new MeasurementService(),
            text,
            MeasurementKindEnum.Length,
            12m,
            WeightUnitEnum.Kg,
            LengthUnitEnum.Cm);
        sut.SetExactText("nope");

        // Act
        text.SetCulture("fr-FR");

        // Assert
        sut.ValidationMessage.Should().Be(text["MeasureInvalid"]);
        sut.HasValidationMessage.Should().BeTrue();
    }
}
