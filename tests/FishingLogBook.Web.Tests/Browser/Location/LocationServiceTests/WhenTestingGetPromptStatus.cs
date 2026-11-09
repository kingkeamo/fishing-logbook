using AwesomeAssertions;
using FishingLogBook.Web.Browser.Location;

namespace FishingLogBook.Web.Tests.Browser.Location.LocationServiceTests;

public class WhenTestingGetPromptStatus : BaseLocationServiceTest
{
    [Theory]
    [InlineData("denied")]
    [InlineData("unavailable")]
    public async Task ItShouldOfferOnlyEnableLaterWhenLocationCannotBeUsed(string permission)
    {
        // Arrange
        Js.Permission = permission;

        // Act
        var status = await Sut.GetPromptStatusAsync(CancellationToken.None);

        // Assert
        status.Should().Be(new LocationPromptStatus(false, true, false));
    }

    [Fact]
    public async Task ItShouldCaptureOnSaveWithoutAnExplainerWhenPermissionIsGranted()
    {
        // Arrange
        Js.Permission = "granted";
        Js.Dismissed = true;

        // Act
        var status = await Sut.GetPromptStatusAsync(CancellationToken.None);

        // Assert
        status.Should().Be(new LocationPromptStatus(false, false, true));
    }

    [Fact]
    public async Task ItShouldShowTheExplainerWhenPermissionHasNotBeenAskedAndIsNotDismissed()
    {
        // Arrange
        Js.Permission = "prompt";
        Js.Dismissed = false;

        // Act
        var status = await Sut.GetPromptStatusAsync(CancellationToken.None);

        // Assert
        status.Should().Be(new LocationPromptStatus(true, false, false));
    }

    [Fact]
    public async Task ItShouldNotNagWhenTheExplainerWasDismissed()
    {
        // Arrange
        Js.Permission = "prompt";
        Js.Dismissed = true;

        // Act
        var status = await Sut.GetPromptStatusAsync(CancellationToken.None);

        // Assert
        status.Should().Be(new LocationPromptStatus(false, true, false));
    }

    [Fact]
    public async Task ItShouldTreatABlankPermissionAsNotYetAsked()
    {
        // Arrange
        Js.Permission = " ";
        Js.Dismissed = false;

        // Act
        var status = await Sut.GetPromptStatusAsync(CancellationToken.None);

        // Assert
        status.ShowExplainer.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRememberADismissedPrompt()
    {
        // Arrange
        // Act
        await Sut.DismissPromptAsync(CancellationToken.None);

        // Assert
        Js.Invoked.Should().Equal("setPromptDismissed");
    }
}
