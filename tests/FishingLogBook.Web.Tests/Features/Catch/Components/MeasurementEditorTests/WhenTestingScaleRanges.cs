using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;
using FishingLogBook.Web.Features.Catch.Enums;
using FishingLogBook.Web.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.MeasurementEditorTests;

public class WhenTestingScaleRanges : BaseMeasurementEditorTest
{
    [Fact]
    public void ItShouldExposeFourRangesWithTheIntendedCanonicalMaxima()
    {
        // Arrange
        var expected = new (MeasurementScaleRangeEnum Range, decimal Maximum)[]
        {
            (MeasurementScaleRangeEnum.Small, 5m),
            (MeasurementScaleRangeEnum.Medium, 15m),
            (MeasurementScaleRangeEnum.Large, 40m),
            (MeasurementScaleRangeEnum.Monster, 120m)
        };

        // Act
        var ranges = MeasurementScaleRangeModel.Weights;

        // Assert
        ranges.Should().HaveCount(4);
        ranges.Select(range => (range.Range, range.MaximumCanonical)).Should().Equal(expected);
    }

    [Theory]
    [InlineData(2, nameof(MeasurementScaleRangeEnum.Small))]
    [InlineData(8, nameof(MeasurementScaleRangeEnum.Medium))]
    [InlineData(30, nameof(MeasurementScaleRangeEnum.Large))]
    [InlineData(80, nameof(MeasurementScaleRangeEnum.Monster))]
    public async Task ItShouldOpenAnExistingWeightInTheSmallestRangeThatContainsIt(
        int canonicalKilograms,
        string expectedRange)
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, Weight(canonicalKilograms));

        // Assert
        cut.Find($"#measurement-range-{expectedRange}").GetAttribute("aria-pressed")
            .Should().Be("true");
        cut.FindAll("[aria-pressed=true]").Should().HaveCount(1);
    }

    [Fact]
    public async Task ItShouldNeverChangeTheCapturedWeightWhileTheRangeChanges()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, Weight(2.268m, WeightUnitEnum.Lb));
        var originalPounds = cut.Find("#measurement-exact-pounds").GetAttribute("value");
        var originalOunces = cut.Find("#measurement-exact-ounces").GetAttribute("value");

        // Act
        foreach (var range in new[] { "Medium", "Large", "Monster", "Small" })
        {
            await cut.Find($"#measurement-range-{range}").ClickAsync();
            cut.Find("#measurement-exact-pounds").GetAttribute("value").Should().Be(originalPounds);
            cut.Find("#measurement-exact-ounces").GetAttribute("value").Should().Be(originalOunces);
        }

        await cut.Find("#measurement-apply").ClickAsync();

        // Assert
        var result = await dialog.Result;
        var applied = (MeasurementEditorResult)result!.Data!;
        applied.CanonicalValue.Should().Be(2.268m);
    }

    [Fact]
    public async Task ItShouldMoveTheNeedleAndSliderMaximumWhenTheRangeChanges()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, _) = await ShowAsync(context, Weight(4m));
        var smallAngle = cut.Find(".measurement-dial-needle").GetAttribute("transform");
        var smallMaximum = cut.Find("#measurement-slider-maximum").TextContent;

        // Act
        await cut.Find("#measurement-range-Large").ClickAsync();

        // Assert
        var largeAngle = cut.Find(".measurement-dial-needle").GetAttribute("transform");
        largeAngle.Should().NotBe(smallAngle);
        smallAngle.Should().Be("rotate(287.2 120 70)");
        largeAngle.Should().Be("rotate(35.9 120 70)");
        cut.Find("#measurement-slider-maximum").TextContent.Should().NotBe(smallMaximum);
        cut.Find("#measurement-exact-value").GetAttribute("value").Should().Be("4");
    }

    [Fact]
    public async Task ItShouldLabelTheSliderBoundsInMetricUnits()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, Weight(1m));

        // Assert
        cut.Find("#measurement-slider-minimum").TextContent.Trim().Should().Be("0 kg");
        cut.Find("#measurement-slider-maximum").TextContent.Trim().Should().Be("5 kg");
        await cut.Find("#measurement-range-Monster").ClickAsync();
        cut.Find("#measurement-slider-maximum").TextContent.Trim().Should().Be("120 kg");
    }

    [Fact]
    public async Task ItShouldLabelTheSliderBoundsInImperialUnitsForTheSamePhysicalRange()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, Weight(1m, WeightUnitEnum.Lb));

        // Assert
        cut.Find("#measurement-slider-minimum").TextContent.Should().Contain("0 lb");
        cut.Find("#measurement-slider-maximum").TextContent.Should().Contain("11 lb");
        cut.Find("#measurement-range-Small").GetAttribute("aria-pressed").Should().Be("true");
        await cut.Find("#measurement-range-Monster").ClickAsync();
        cut.Find("#measurement-slider-maximum").TextContent.Should().Contain("264 lb");
    }

    [Theory]
    [InlineData("10", nameof(MeasurementScaleRangeEnum.Medium))]
    [InlineData("35", nameof(MeasurementScaleRangeEnum.Large))]
    [InlineData("100", nameof(MeasurementScaleRangeEnum.Monster))]
    public async Task ItShouldExpandToTheSmallestRangeThatCanDisplayAnExactEntry(
        string exactValue,
        string expectedRange)
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, Weight(1m));
        cut.Find("#measurement-range-Small").GetAttribute("aria-pressed").Should().Be("true");

        // Act
        cut.Find("#measurement-exact-value").Input(exactValue);

        // Assert
        cut.Find($"#measurement-range-{expectedRange}").GetAttribute("aria-pressed")
            .Should().Be("true");
        await cut.Find("#measurement-apply").ClickAsync();
        var result = await dialog.Result;
        var applied = (MeasurementEditorResult)result!.Data!;
        applied.CanonicalValue.Should().Be(decimal.Parse(exactValue));
    }

    [Fact]
    public async Task ItShouldExpandTheRangeWhenFineAdjustmentCrossesTheMaximum()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, Weight(4.95m));
        cut.Find("#measurement-range-Small").GetAttribute("aria-pressed").Should().Be("true");

        // Act
        await cut.Find("#measurement-increase").ClickAsync();

        // Assert
        cut.Find("#measurement-range-Medium").GetAttribute("aria-pressed").Should().Be("true");
        await cut.Find("#measurement-apply").ClickAsync();
        var result = await dialog.Result;
        var applied = (MeasurementEditorResult)result!.Data!;
        applied.CanonicalValue.Should().Be(5.05m);
    }

    [Fact]
    public async Task ItShouldKeepAnExplicitlyChosenLargerRangeWhenTheWeightDecreases()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, _) = await ShowAsync(context, Weight(1m));
        await cut.Find("#measurement-range-Monster").ClickAsync();

        // Act
        await cut.Find("#measurement-decrease").ClickAsync();

        // Assert
        cut.Find("#measurement-range-Monster").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("#measurement-range-Small").GetAttribute("aria-pressed").Should().Be("false");
    }

    [Fact]
    public async Task ItShouldNotClampTheWeightToTheVisualMaximum()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, Weight(1m));

        // Act
        cut.Find("#measurement-exact-value").Input("300");

        // Assert
        cut.Find("#measurement-range-Monster").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("#measurement-exact-value").GetAttribute("value").Should().Be("300");
        await cut.Find("#measurement-apply").ClickAsync();
        var result = await dialog.Result;
        var applied = (MeasurementEditorResult)result!.Data!;
        applied.CanonicalValue.Should().Be(300m);
    }

    [Fact]
    public async Task ItShouldDisableRangesThatCannotRepresentTheCurrentWeight()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, Weight(30m));

        // Assert
        cut.Find("#measurement-range-Small").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#measurement-range-Medium").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#measurement-range-Large").HasAttribute("disabled").Should().BeFalse();
        cut.Find("#measurement-range-Monster").HasAttribute("disabled").Should().BeFalse();
        await cut.Find("#measurement-range-Small").ClickAsync();
        cut.Find("#measurement-range-Large").GetAttribute("aria-pressed").Should().Be("true");
    }

    [Fact]
    public async Task ItShouldKeepTheSamePhysicalRangeAndWeightAcrossDisplayUnits()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (metric, _) = await ShowAsync(context, Weight(3m));
        var (imperial, _) = await ShowAsync(context, Weight(3m, WeightUnitEnum.Lb));

        // Assert
        metric.Find("#measurement-range-Small").GetAttribute("aria-pressed").Should().Be("true");
        imperial.FindAll("#measurement-range-Small").Last().GetAttribute("aria-pressed")
            .Should().Be("true");
        metric.Find("#measurement-slider-maximum").TextContent.Trim().Should().Be("5 kg");
        imperial.FindAll("#measurement-slider-maximum").Last().TextContent.Should().Contain("11 lb");
    }

    [Fact]
    public async Task ItShouldNotAffectClearOrCancelSemantics()
    {
        // Arrange
        await using var context = CreateContext();
        var (cleared, clearedDialog) = await ShowAsync(context, Weight(2m));
        await cleared.Find("#measurement-range-Monster").ClickAsync();

        // Act
        await cleared.Find("#measurement-clear").ClickAsync();
        await cleared.Find("#measurement-apply").ClickAsync();
        var (cancelled, cancelledDialog) = await ShowAsync(context, Weight(2m));
        await cancelled.FindAll("#measurement-range-Large").Last().ClickAsync();
        await cancelled.FindAll("#measurement-cancel").Last().ClickAsync();

        // Assert
        var clearedResult = await clearedDialog.Result;
        ((MeasurementEditorResult)clearedResult!.Data!).CanonicalValue.Should().BeNull();
        var cancelledResult = await cancelledDialog.Result;
        cancelledResult!.Canceled.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldExposeAccessibleNamesAndSelectionStateForEveryRange()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, Weight(1m));

        // Assert
        cut.Find("#measurement-ranges").GetAttribute("role").Should().Be("group");
        foreach (var (range, label) in new[]
                 {
                     ("Small", "Small"), ("Medium", "Medium"), ("Large", "Large"), ("Monster", "Monster")
                 })
        {
            var button = cut.Find($"#measurement-range-{range}");
            button.TagName.Should().Be("BUTTON");
            button.GetAttribute("type").Should().Be("button");
            button.GetAttribute("aria-label").Should().Be(label);
            button.HasAttribute("aria-pressed").Should().BeTrue();
        }

        cut.Find("#measurement-range-Small").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find(".measurement-range-selected").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldPlaceTheRangeSelectorBesideTheScaleLargestFirst()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, Weight(1m));

        // Assert
        cut.Find(".measurement-scale-layout").Should().NotBeNull();
        cut.Find(".measurement-scale-layout .measurement-ranges").Should().NotBeNull();
        cut.Find(".measurement-scale-layout .measurement-scale").Should().NotBeNull();
        cut.FindAll(".measurement-range").Select(button => button.Id)
            .Should().Equal(
                "measurement-range-Monster",
                "measurement-range-Large",
                "measurement-range-Medium",
                "measurement-range-Small");
    }

    [Fact]
    public void ItShouldExposeFourLengthRangesWithTheIntendedCanonicalMaxima()
    {
        // Arrange
        var expected = new (MeasurementScaleRangeEnum Range, decimal Maximum)[]
        {
            (MeasurementScaleRangeEnum.Small, 30m),
            (MeasurementScaleRangeEnum.Medium, 60m),
            (MeasurementScaleRangeEnum.Large, 120m),
            (MeasurementScaleRangeEnum.Monster, 300m)
        };

        // Act
        var ranges = MeasurementScaleRangeModel.Lengths;

        // Assert
        ranges.Should().HaveCount(4);
        ranges.Select(range => (range.Range, range.MaximumCanonical)).Should().Equal(expected);
    }

    [Fact]
    public async Task ItShouldOfferTheSameRangeSelectorForLengthBesideTheTape()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, Length(50m));

        // Assert
        cut.Find(".measurement-scale-layout .measurement-tape").Should().NotBeNull();
        cut.Find("#measurement-range-Medium").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("#measurement-slider-minimum").TextContent.Trim().Should().Be("0 cm");
        cut.Find("#measurement-slider-maximum").TextContent.Trim().Should().Be("60 cm");
        cut.FindAll(".measurement-range").Should().HaveCount(4);
        cut.Find("#measurement-range-Small").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldExpandTheLengthRangeWithoutChangingTheCapturedLength()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, Length(20m));
        cut.Find("#measurement-range-Small").GetAttribute("aria-pressed").Should().Be("true");
        var tapeAtSmall = cut.Find(".measurement-tape-strip").GetAttribute("width");

        // Act
        cut.Find("#measurement-exact-value").Input("70");

        // Assert
        cut.Find("#measurement-range-Large").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find(".measurement-tape-strip").GetAttribute("width").Should().NotBe(tapeAtSmall);
        await cut.Find("#measurement-apply").ClickAsync();
        var result = await dialog.Result;
        ((MeasurementEditorResult)result!.Data!).CanonicalValue.Should().Be(70m);
    }

    [Fact]
    public async Task ItShouldLabelLengthBoundsInImperialUnitsForTheSamePhysicalRange()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, Length(20m, LengthUnitEnum.In));

        // Assert
        cut.Find("#measurement-range-Small").GetAttribute("aria-pressed").Should().Be("true");
        cut.Find("#measurement-slider-minimum").TextContent.Trim().Should().Be("0 in");
        cut.Find("#measurement-slider-maximum").TextContent.Should().Contain("11.81 in");
    }
}
