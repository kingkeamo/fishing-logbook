using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Components.CatchSelector;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchSelectorTests;

public class WhenTestingSelect : BaseCatchSelectorTest
{
    [Fact]
    public async Task ItShouldRenderNothingWhenThereIsNothingToSelect()
    {
        // Arrange
        await using var context = CreateContext();
        var selections = new List<IReadOnlyList<Guid>>();

        // Act
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, Array.Empty<CatchModel>())
            .Add(component => component.SelectedChanged, ids => selections.Add(ids)));

        // Assert
        cut.FindAll(".catch-selector-row").Should().BeEmpty();
        selections.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotSelectAnythingUntilTheAnglerChooses()
    {
        // Arrange
        await using var context = CreateContext();
        var selections = new List<IReadOnlyList<Guid>>();

        // Act
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { Catch(PikeCatchId, "Pike") })
            .Add(component => component.SelectedChanged, ids => selections.Add(ids)));

        // Assert
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull();
        selections.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotChangeTheSelectionWhileDisabled()
    {
        // Arrange
        await using var context = CreateContext();
        var selections = new List<IReadOnlyList<Guid>>();
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { Catch(PikeCatchId, "Pike") })
            .Add(component => component.Disabled, true)
            .Add(component => component.SelectedChanged, ids => selections.Add(ids)));

        // Act
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);

        // Assert
        selections.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldForgetASelectionThatIsNoLongerOffered()
    {
        // Arrange
        await using var context = CreateContext();
        var selections = new List<IReadOnlyList<Guid>>();
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { Catch(PikeCatchId, "Pike") })
            .Add(component => component.SelectedChanged, ids => selections.Add(ids)));
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);
        selections.Should().ContainSingle();

        // Act
        cut.Render(parameters => parameters
            .Add(component => component.Catches, new[] { Catch(TroutCatchId, "Brown Trout") })
            .Add(component => component.SelectedChanged, ids => selections.Add(ids)));

        // Assert
        selections.Last().Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldDeselectACatchThatWasSelectedByMistake()
    {
        // Arrange
        await using var context = CreateContext();
        var selections = new List<IReadOnlyList<Guid>>();
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { Catch(PikeCatchId, "Pike") })
            .Add(component => component.SelectedChanged, ids => selections.Add(ids)));
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);

        // Act
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(false);

        // Assert
        selections.Should().HaveCount(2);
        selections[0].Should().Equal(PikeCatchId);
        selections[1].Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReportEverySelectedCatch()
    {
        // Arrange
        await using var context = CreateContext();
        var selections = new List<IReadOnlyList<Guid>>();
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[]
            {
                Catch(PikeCatchId, "Pike"),
                Catch(TroutCatchId, "Brown Trout")
            })
            .Add(component => component.SelectedChanged, ids => selections.Add(ids)));

        // Act
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);
        cut.Find($"#catch-selector-option-{TroutCatchId:D}").Change(true);

        // Assert
        selections.Last().Should().Equal(PikeCatchId, TroutCatchId);
    }

    [Fact]
    public async Task ItShouldDescribeACatchWithoutRenderingEmptyValues()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[]
            {
                Catch(PikeCatchId, "Pike", weight: 2.5m),
                Catch(TroutCatchId, null, length: 45m)
            })
            .Add(component => component.UnknownSpeciesLabel, "Unnamed catch")
            .Add(component => component.WeightUnit, WeightUnitEnum.Kg)
            .Add(component => component.LengthUnit, LengthUnitEnum.Cm));

        // Assert
        cut.Find($"#catch-selector-species-{PikeCatchId:D}").TextContent.Should().Contain("Pike");
        cut.Find($"#catch-selector-facts-{PikeCatchId:D}").TextContent.Should().Contain("2.5 kg");
        cut.Find($"#catch-selector-facts-{PikeCatchId:D}").TextContent.Should().NotContain("cm");
        cut.Find($"#catch-selector-species-{TroutCatchId:D}").TextContent.Should().Contain("Unnamed catch");
        cut.Find($"#catch-selector-facts-{TroutCatchId:D}").TextContent.Should().Contain("45 cm");
        cut.FindAll($"#catch-selector-method-{TroutCatchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheMethodWhenTheAnglerRecordedOne()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { Catch(PikeCatchId, "Pike", method: "Fly") }));

        // Assert
        cut.Find($"#catch-selector-method-{PikeCatchId:D}").TextContent.Should().Contain("Fly");
    }

    [Fact]
    public async Task ItShouldShowTheLocalTimeOfTheCatch()
    {
        // Arrange
        await using var context = CreateContext(time: TestTimeService.WithOffset(TimeSpan.FromHours(2)));

        // Act
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { Catch(PikeCatchId, "Pike") }));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-selector-facts-{PikeCatchId:D}").TextContent.Should().Contain("09:30"));
    }
}
