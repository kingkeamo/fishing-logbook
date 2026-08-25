using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.MeasurementEditorTests;

public class WhenTestingEditing : BaseMeasurementEditorTest
{
    [Fact]
    public async Task ItShouldNotApplyAnInvalidExactValue()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, Weight(2.5m));
        cut.Find("#measurement-exact-value").Input("invalid");

        // Act
        await cut.Find("#measurement-apply").ClickAsync();

        // Assert
        cut.Find("#measurement-validation").Should().NotBeNull();
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRenderTheWeightDialAtTheCurrentValue()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, Weight(50m));

        // Assert
        cut.Find(".measurement-dial").Should().NotBeNull();
        cut.Find(".measurement-dial-needle").GetAttribute("transform").Should().Be("rotate(179.5 120 70)");
        cut.FindAll(".measurement-dial-tick").Should().HaveCount(24);
        cut.Find(".measurement-scale-hook").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldExpandTheLengthTapeWithTheCurrentValue()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, _) = await ShowAsync(context, Length());
        var initialWidth = cut.Find(".measurement-tape-strip").GetAttribute("width");

        // Act
        cut.Find("#measurement-slider").Input("150");

        // Assert
        cut.Find(".measurement-tape-visual").Should().NotBeNull();
        cut.Find(".measurement-tape-logo").GetAttribute("href").Should().Be("images/brand/brand-mark-transparent.png");
        cut.Find(".measurement-tape-strip").GetAttribute("width").Should().NotBe(initialWidth);
    }

    [Fact]
    public async Task ItShouldCancelWithoutApplyingAChange()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, Weight(2.5m));

        // Act
        await cut.Find("#measurement-increase").ClickAsync();
        await cut.Find("#measurement-cancel").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result.Should().NotBeNull();
        result!.Canceled.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldClearAnExistingMeasurement()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, Length(64m));

        // Act
        await cut.Find("#measurement-clear").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result.Should().NotBeNull();
        result!.Canceled.Should().BeFalse();
        result.Data.Should().BeOfType<MeasurementEditorResult>().Which.CanonicalValue.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldApplyExactMetricWeight()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, Weight());
        cut.Find("#measurement-exact-value").Input("3.75");

        // Act
        await cut.Find("#measurement-apply").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result.Should().NotBeNull();
        result!.Data.Should().BeOfType<MeasurementEditorResult>().Which.CanonicalValue.Should().Be(3.75m);
    }

    [Fact]
    public async Task ItShouldRollImperialWeightFromFifteenOuncesToTheNextPound()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new FishingLogBook.Web.Features.Catch.Services.MeasurementService();
        var starting = service.FromPoundsAndOunces(3, 15);
        var (cut, dialog) = await ShowAsync(context, Weight(starting, FishingLogBook.Shared.Enums.WeightUnitEnum.Lb));

        // Act
        await cut.Find("#measurement-increase").ClickAsync();
        await cut.Find("#measurement-apply").ClickAsync();
        var result = await dialog.Result;

        // Assert
        cut.FindAll("#measurement-editor-modal").Should().BeEmpty();
        var canonical = result!.Data.Should().BeOfType<MeasurementEditorResult>().Which.CanonicalValue;
        service.ToPoundsAndOunces(canonical).Should().Be((4, 0));
    }

    [Fact]
    public async Task ItShouldApplySixteenImperialFineAdjustmentsAsOnePound()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new FishingLogBook.Web.Features.Catch.Services.MeasurementService();
        var (cut, dialog) = await ShowAsync(context, Weight(null, FishingLogBook.Shared.Enums.WeightUnitEnum.Lb));

        // Act
        for (var adjustment = 0; adjustment < 16; adjustment++)
        {
            await cut.Find("#measurement-increase").ClickAsync();
        }
        await cut.Find("#measurement-apply").ClickAsync();
        var result = await dialog.Result;

        // Assert
        var canonical = result!.Data.Should().BeOfType<MeasurementEditorResult>().Which.CanonicalValue;
        service.ToPoundsAndOunces(canonical).Should().Be((1, 0));
    }

    [Fact]
    public async Task ItShouldKeepTheSliderAndExactEntrySynchronized()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, Length());

        // Act
        cut.Find("#measurement-slider").Input("72");
        var exactValue = cut.Find("#measurement-exact-value").GetAttribute("value");
        var tapeWidth = cut.Find(".measurement-tape-strip").GetAttribute("width");
        await cut.Find("#measurement-apply").ClickAsync();
        var result = await dialog.Result;

        // Assert
        exactValue.Should().Be("72");
        tapeWidth.Should().NotBe("12.0");
        result!.Data.Should().BeOfType<MeasurementEditorResult>().Which.CanonicalValue.Should().Be(72m);
    }
}
