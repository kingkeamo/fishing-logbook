using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Components.CatchSelector;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchSelectorTests;

public class WhenTestingSelect : BaseCatchSelectorTest
{
    [Fact]
    public async Task ItShouldShowTheEmptyLabelAndNoConfirmActionWhenThereIsNothingToSelect()
    {
        // Arrange
        await using var context = CreateContext();
        var confirmed = new List<IReadOnlyList<Guid>>();

        // Act
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, Array.Empty<CatchModel>())
            .Add(component => component.ConfirmLabel, "Add to this trip")
            .Add(component => component.EmptyLabel, "Nothing to add")
            .Add(component => component.OnConfirm, ids => confirmed.Add(ids)));

        // Assert
        cut.Find("#catch-selector-empty").TextContent.Should().Contain("Nothing to add");
        cut.FindAll("#catch-selector-confirm").Should().BeEmpty();
        confirmed.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepConfirmDisabledUntilSomethingIsSelected()
    {
        // Arrange
        await using var context = CreateContext();
        var confirmed = new List<IReadOnlyList<Guid>>();
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { Catch(PikeCatchId, "Pike") })
            .Add(component => component.ConfirmLabel, "Add to this trip")
            .Add(component => component.EmptyLabel, "Nothing to add")
            .Add(component => component.OnConfirm, ids => confirmed.Add(ids)));

        // Act
        cut.Find("#catch-selector-confirm").Click();

        // Assert
        cut.Find("#catch-selector-confirm").HasAttribute("disabled").Should().BeTrue();
        confirmed.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotConfirmWhenTheSelectorIsDisabled()
    {
        // Arrange
        await using var context = CreateContext();
        var confirmed = new List<IReadOnlyList<Guid>>();
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { Catch(PikeCatchId, "Pike") })
            .Add(component => component.ConfirmLabel, "Add to this trip")
            .Add(component => component.EmptyLabel, "Nothing to add")
            .Add(component => component.Disabled, true)
            .Add(component => component.OnConfirm, ids => confirmed.Add(ids)));

        // Act
        cut.Find("#catch-selector-confirm").Click();

        // Assert
        confirmed.Should().BeEmpty();
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldForgetASelectionThatIsNoLongerOffered()
    {
        // Arrange
        await using var context = CreateContext();
        var confirmed = new List<IReadOnlyList<Guid>>();
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(
                component => component.Catches,
                new[] { Catch(PikeCatchId, "Pike"), Catch(TroutCatchId, "Brown Trout") })
            .Add(component => component.ConfirmLabel, "Add to this trip")
            .Add(component => component.EmptyLabel, "Nothing to add")
            .Add(component => component.OnConfirm, ids => confirmed.Add(ids)));
        cut.Find($"#catch-selector-option-{TroutCatchId:D}").Change(true);

        // Act
        cut.Render(parameters => parameters
            .Add(component => component.Catches, new[] { Catch(PikeCatchId, "Pike") }));
        cut.Find("#catch-selector-confirm").Click();

        // Assert
        confirmed.Should().BeEmpty();
        cut.Find("#catch-selector-confirm").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldDeselectACatchThatWasSelectedByMistake()
    {
        // Arrange
        await using var context = CreateContext();
        var confirmed = new List<IReadOnlyList<Guid>>();
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(
                component => component.Catches,
                new[] { Catch(PikeCatchId, "Pike"), Catch(TroutCatchId, "Brown Trout") })
            .Add(component => component.ConfirmLabel, "Add to this trip")
            .Add(component => component.EmptyLabel, "Nothing to add")
            .Add(component => component.OnConfirm, ids => confirmed.Add(ids)));
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);
        cut.Find($"#catch-selector-option-{TroutCatchId:D}").Change(true);

        // Act
        cut.Find($"#catch-selector-option-{TroutCatchId:D}").Change(false);
        cut.Find("#catch-selector-confirm").Click();

        // Assert
        confirmed.Should().ContainSingle();
        confirmed[0].Should().Equal(PikeCatchId);
    }

    [Fact]
    public async Task ItShouldConfirmEverySelectedCatchAndDescribeThemForTheAngler()
    {
        // Arrange
        await using var context = CreateContext();
        var confirmed = new List<IReadOnlyList<Guid>>();
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(
                component => component.Catches,
                new[]
                {
                    Catch(PikeCatchId, "Pike"),
                    Catch(TroutCatchId, null, CaughtOn.AddHours(1))
                })
            .Add(component => component.ConfirmLabel, "Add to this trip")
            .Add(component => component.EmptyLabel, "Nothing to add")
            .Add(component => component.UnknownSpeciesLabel, "Catch recorded")
            .Add(component => component.OnConfirm, ids => confirmed.Add(ids)));

        // Act
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);
        cut.Find($"#catch-selector-option-{TroutCatchId:D}").Change(true);
        cut.Find("#catch-selector-confirm").Click();

        // Assert
        confirmed.Should().ContainSingle();
        confirmed[0].Should().Equal(PikeCatchId, TroutCatchId);
        cut.Markup.Should().Contain("Pike");
        cut.Markup.Should().Contain("Catch recorded");
    }
}
