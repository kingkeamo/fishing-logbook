using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.SystemStatus.Components.StatusRow;
using FishingLogBook.Web.Features.SystemStatus.Models;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.SystemStatus.Components.StatusRowTests;

public class WhenTestingRender
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    [Theory]
    [InlineData(StatusState.Online, "Online", "mud-chip-color-success")]
    [InlineData(StatusState.Degraded, "Degraded", "mud-chip-color-warning")]
    [InlineData(StatusState.Offline, "Offline", "mud-chip-color-error")]
    [InlineData(StatusState.Checking, "Checking", "mud-chip-color-default")]
    public async Task ItShouldShowTheStateWithItsColour(StatusState state, string text, string colourClass)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<StatusRow>(parameters => parameters
            .Add(component => component.Id, "api")
            .Add(component => component.Label, "API")
            .Add(component => component.State, state));

        // Assert
        cut.Find("#status-row-api").TextContent.Should().Contain("API").And.Contain(text);
        cut.Find("#status-row-api .mud-chip").ClassList.Should().Contain(colourClass);
        cut.FindAll("#status-row-api .mud-progress-circular").Should().HaveCount(state == StatusState.Checking ? 1 : 0);
    }

    [Theory]
    [InlineData("FishingLogBook database online", true)]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    public async Task ItShouldShowTheDetailOnlyWhenThereIsSome(string? detail, bool shown)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<StatusRow>(parameters => parameters
            .Add(component => component.Id, "database")
            .Add(component => component.Label, "Database")
            .Add(component => component.State, StatusState.Online)
            .Add(component => component.DetailText, detail));

        // Assert
        cut.FindAll("#status-row-database .status-row-detail").Should().HaveCount(shown ? 1 : 0);
        if (shown)
        {
            cut.Find("#status-row-database .status-row-detail").TextContent.Should().Be(detail);
        }
    }
}
