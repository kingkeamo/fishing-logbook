using System.Globalization;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Measurements.Models;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Tests.Features.Measurements.ViewModels.MeasurementEditorViewModelTests;

public class WhenTestingMeasurementEditorLifecycle
{
    private static MeasurementEditorViewModel Create(UiTextService text, decimal? canonical = null)
    {
        return new MeasurementEditorViewModel(new MeasurementService(), text, MeasurementKindEnum.Weight, canonical, WeightUnitEnum.Kg, LengthUnitEnum.Cm);
    }

    [Fact]
    public void ItShouldRaiseChangeNotificationsOnCultureChangeBeforeDisposal()
    {
        // Arrange
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        using var sut = Create(text);
        var raised = 0;
        sut.PropertyChanged += (_, _) => raised++;
        // Act
        text.SetCulture("fr-FR");
        // Assert
        raised.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ItShouldStopRespondingToCultureChangesAfterDisposal()
    {
        // Arrange
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        var sut = Create(text);
        var raised = 0;
        sut.PropertyChanged += (_, _) => raised++;
        // Act
        sut.Dispose();
        text.SetCulture("fr-FR");
        // Assert
        raised.Should().Be(0);
    }

    [Fact]
    public void ItShouldAllowRepeatedDisposal()
    {
        // Arrange
        var sut = Create(new UiTextService(CultureInfo.GetCultureInfo("en-GB")));
        // Act
        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };
        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ItShouldAllowADisposedViewModelToBeCollectedWhileTheTextServiceLives()
    {
        // Arrange
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        var reference = CreateAndDispose(text);
        // Act
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        // Assert
        reference.IsAlive.Should().BeFalse();
        GC.KeepAlive(text);
    }

    [Fact]
    public void ItShouldRetainAnUndisposedViewModelViaTheTextService()
    {
        // Arrange
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        var reference = CreateWeak(text);
        // Act
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        // Assert
        reference.IsAlive.Should().BeTrue();
        GC.KeepAlive(text);
    }

    [Fact]
    public void ItShouldNotLeakAnySubscriptionsWhenManyViewModelsAreCreatedAndDisposed()
    {
        // Arrange
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        var references = new List<WeakReference>();
        // Act
        for (var index = 0; index < 50; index++)
        {
            references.Add(CreateAndDispose(text));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        // Assert
        references.Count(reference => reference.IsAlive).Should().Be(0);
        GC.KeepAlive(text);
    }

    [Theory]
    [InlineData(MeasurementScaleRangeEnum.Small, true, "Small, selected")]
    [InlineData(MeasurementScaleRangeEnum.Medium, true, "Medium")]
    public void ItShouldDescribeRangeSelectionForAccessibility(MeasurementScaleRangeEnum range, bool weight, string expected)
    {
        // Arrange
        using var sut = Create(new UiTextService(CultureInfo.GetCultureInfo("en-GB")));
        var model = MeasurementScaleRangeModel.For(weight).Single(item => item.Range == range);
        // Act
        var description = sut.RangeDescription(model);
        // Assert
        description.Should().Be(expected);
    }

    [Fact]
    public void ItShouldDescribeRangesThatCannotShowTheValueAsUnavailable()
    {
        // Arrange
        using var sut = Create(new UiTextService(CultureInfo.GetCultureInfo("en-GB")), 9m);
        var small = MeasurementScaleRangeModel.For(true).Single(item => item.Range == MeasurementScaleRangeEnum.Small);
        // Act
        var description = sut.RangeDescription(small);
        // Assert
        description.Should().Be("Small, unavailable for this value");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndDispose(UiTextService text)
    {
        var vm = Create(text);
        var reference = new WeakReference(vm);
        vm.Dispose();
        return reference;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateWeak(UiTextService text)
    {
        return new WeakReference(Create(text));
    }
}
