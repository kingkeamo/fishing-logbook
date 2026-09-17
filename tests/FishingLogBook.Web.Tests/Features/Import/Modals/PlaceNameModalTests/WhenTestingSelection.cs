using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Import.Modals.PlaceName;

namespace FishingLogBook.Web.Tests.Features.Import.Modals.PlaceNameModalTests;

public class WhenTestingSelection : BasePlaceNameModalTest
{
    [Fact]
    public async Task ItShouldSelectEveryUsefulComponentByDefault()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, DefaultModel());

        // Assert
        cut.Find("#place-name-preview").TextContent.Should().Contain(
            "42 Street 7, Zayed International Airport, Abu Dhabi, United Arab Emirates");
        cut.FindAll("input[type=checkbox]").Should().OnlyContain(input => input.HasAttribute("checked"));
    }

    [Fact]
    public async Task ItShouldUpdateThePreviewAndReturnTheSelectedComponents()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, DefaultModel());

        // Act
        await cut.Find("#place-name-component-0").ChangeAsync(false);
        cut.Find("#place-name-preview").TextContent.Should().NotContain("42 Street 7");
        await cut.Find("#place-name-apply").ClickAsync();
        var result = await dialog.Result;

        // Assert
        ((PlaceNameModalResult)result!.Data!).PlaceName.Should().Be(
            "Zayed International Airport, Abu Dhabi, United Arab Emirates");
    }

    [Fact]
    public async Task ItShouldReturnNullWhenEveryComponentIsDeselected()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, DefaultModel());

        // Act
        foreach (var checkbox in cut.FindAll("input[type=checkbox]"))
        {
            await checkbox.ChangeAsync(false);
        }
        await cut.Find("#place-name-apply").ClickAsync();
        var result = await dialog.Result;

        // Assert
        ((PlaceNameModalResult)result!.Data!).PlaceName.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldCancelWithoutReturningAChangedPlaceName()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, DefaultModel("Abu Dhabi"));

        // Act
        await cut.Find("#place-name-component-0").ChangeAsync(true);
        await cut.Find("#place-name-cancel").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result!.Canceled.Should().BeTrue();
        result.Data.Should().BeNull();
    }
}
