using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Photographs.Components.PhotographGridSelector;
using FishingLogBook.Web.Features.Photographs.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Photographs.Components.PhotographGridSelectorTests;

public class WhenTestingRender
{
    [Fact]
    public async Task ItShouldHideSelectedActionsUntilAPhotographIsSelected()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = RenderSelector(context, Photographs());

        // Assert
        cut.FindAll("#grid-selected-actions").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowActionsBesideTheGridAndRaiseTheSelectedIdentities()
    {
        // Arrange
        await using var context = CreateContext();
        var photographs = Photographs();
        IReadOnlySet<Guid> selectedIds = new HashSet<Guid>();
        var cut = RenderSelector(context, photographs, ids => selectedIds = ids.ToHashSet());

        // Act
        cut.Find("#grid-select-1").Change(true);

        // Assert
        selectedIds.Should().BeEquivalentTo([photographs[1].Id]);
        cut.Find("#grid-selected-actions").TextContent.Should().Contain("1 selected");
        cut.Find("#grid-selected-actions").PreviousElementSibling!.ClassList
            .Should().Contain("photograph-grid-selector-grid");
    }

    [Fact]
    public async Task ItShouldRenderLocalBytesAndRemoteUrls()
    {
        // Arrange
        await using var context = CreateContext();
        var photographs = new[]
        {
            new PhotographCarouselItemModel(Guid.NewGuid(), "image/jpeg", [1, 2, 3]),
            new PhotographCarouselItemModel(Guid.NewGuid(), "image/jpeg", RemoteUrl: "blob:thumbnail")
        };

        // Act
        var cut = RenderSelector(context, photographs);

        // Assert
        cut.Find("#grid-photo-0").GetAttribute("src").Should().StartWith("data:image/jpeg;base64,");
        cut.Find("#grid-photo-1").GetAttribute("src").Should().Be("blob:thumbnail");
    }

    [Fact]
    public async Task ItShouldRaiseTheOpenedPhotographWithoutChangingSelection()
    {
        // Arrange
        await using var context = CreateContext();
        var photographs = Photographs();
        Guid? activePhotographId = null;
        IReadOnlySet<Guid> selectedIds = new HashSet<Guid>();
        var cut = context.Render<PhotographGridSelector>(parameters => parameters
            .Add(selector => selector.Photographs, photographs)
            .Add(selector => selector.IdPrefix, "grid")
            .Add(selector => selector.ActivePhotographIdChanged,
                EventCallback.Factory.Create<Guid?>(this, id => activePhotographId = id))
            .Add(selector => selector.SelectedIdsChanged,
                EventCallback.Factory.Create<IReadOnlySet<Guid>>(this, ids => selectedIds = ids.ToHashSet())));

        // Act
        cut.Find("#grid-open-1").Click();

        // Assert
        activePhotographId.Should().Be(photographs[1].Id);
        selectedIds.Should().BeEmpty();
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        return context;
    }

    private static IRenderedComponent<PhotographGridSelector> RenderSelector(
        BunitContext context,
        IReadOnlyList<PhotographCarouselItemModel> photographs,
        Action<IReadOnlySet<Guid>>? selectedIdsChanged = null)
    {
        return context.Render<PhotographGridSelector>(parameters => parameters
            .Add(selector => selector.Photographs, photographs)
            .Add(selector => selector.IdPrefix, "grid")
            .Add(selector => selector.SelectedIdsChanged,
                EventCallback.Factory.Create(
                    typeof(WhenTestingRender),
                    selectedIdsChanged ?? (_ => { })))
            .Add(selector => selector.SelectedActions, count => builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddContent(1, $"{count} selected");
                builder.CloseElement();
            }));
    }

    private static IReadOnlyList<PhotographCarouselItemModel> Photographs()
    {
        return
        [
            new PhotographCarouselItemModel(Guid.NewGuid(), "image/jpeg", RemoteUrl: "blob:first"),
            new PhotographCarouselItemModel(Guid.NewGuid(), "image/jpeg", RemoteUrl: "blob:second")
        ];
    }
}
