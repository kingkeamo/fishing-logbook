using AwesomeAssertions;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Shared.Tests.Measurements.MeasurementScaleRangeModelTests;

public class WhenTestingRanges
{
    [Fact]
    public void ItShouldProvideWeightRangesInIncreasingOrder()
    {
        // Arrange
        // Act
        var ranges = MeasurementScaleRangeModel.For(true);

        // Assert
        ranges.Select(range => range.Range).Should().Equal(
            MeasurementScaleRangeEnum.Small,
            MeasurementScaleRangeEnum.Medium,
            MeasurementScaleRangeEnum.Large,
            MeasurementScaleRangeEnum.Monster);
        ranges.Select(range => range.MaximumCanonical).Should().Equal(5m, 15m, 40m, 120m);
    }

    [Fact]
    public void ItShouldProvideLengthRangesInIncreasingOrder()
    {
        // Arrange
        // Act
        var ranges = MeasurementScaleRangeModel.For(false);

        // Assert
        ranges.Select(range => range.MaximumCanonical).Should().Equal(30m, 60m, 120m, 300m);
    }

    [Fact]
    public void ItShouldDeriveTheLabelKeyFromTheRange()
    {
        // Arrange
        var range = new MeasurementScaleRangeModel(MeasurementScaleRangeEnum.Large, 40m);

        // Act
        var key = range.LabelKey;

        // Assert
        key.Should().Be("Catch_MeasurementRange_Large");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("0", true)]
    [InlineData("5", true)]
    [InlineData("5.01", false)]
    public void ItShouldDisplayAValueUpToTheMaximumInclusive(string? value, bool expected)
    {
        // Arrange
        var range = MeasurementScaleRangeModel.Weights[0];
        decimal? canonical = value is null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var result = range.CanDisplay(canonical);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, MeasurementScaleRangeEnum.Small)]
    [InlineData("5", MeasurementScaleRangeEnum.Small)]
    [InlineData("5.01", MeasurementScaleRangeEnum.Medium)]
    [InlineData("15", MeasurementScaleRangeEnum.Medium)]
    [InlineData("40", MeasurementScaleRangeEnum.Large)]
    [InlineData("120", MeasurementScaleRangeEnum.Monster)]
    [InlineData("500", MeasurementScaleRangeEnum.Monster)]
    public void ItShouldChooseTheSmallestWeightRangeThatCanDisplayTheValue(string? value, MeasurementScaleRangeEnum expected)
    {
        // Arrange
        decimal? canonical = value is null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var result = MeasurementScaleRangeModel.SmallestFor(true, canonical);

        // Assert
        result.Range.Should().Be(expected);
    }

    [Fact]
    public void ItShouldChooseTheSmallestLengthRangeThatCanDisplayTheValue()
    {
        // Arrange
        // Act
        var result = MeasurementScaleRangeModel.SmallestFor(false, 61m);

        // Assert
        result.Range.Should().Be(MeasurementScaleRangeEnum.Large);
    }

    [Fact]
    public void ItShouldKeepTheCurrentRangeWhenItCanStillDisplayTheValue()
    {
        // Arrange
        var current = MeasurementScaleRangeModel.Weights[2];

        // Act
        var result = MeasurementScaleRangeModel.ExpandedFor(true, current, 3m);

        // Assert
        result.Should().BeSameAs(current);
    }

    [Fact]
    public void ItShouldExpandToTheSmallestRangeThatCanDisplayTheValueWhenTheCurrentOneCannot()
    {
        // Arrange
        var current = MeasurementScaleRangeModel.Weights[0];

        // Act
        var result = MeasurementScaleRangeModel.ExpandedFor(true, current, 20m);

        // Assert
        result.Range.Should().Be(MeasurementScaleRangeEnum.Large);
    }
}
