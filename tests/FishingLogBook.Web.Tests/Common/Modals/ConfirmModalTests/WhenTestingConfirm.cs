using AwesomeAssertions;
using Bunit;

namespace FishingLogBook.Web.Tests.Common.Modals.ConfirmModalTests;

public class WhenTestingConfirm : BaseConfirmModalTest
{
    [Fact]
    public async Task ItShouldCancelWithoutConfirming()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, DefaultModel());
        cut.Find("#confirm-modal-title").TextContent.Should().Contain("Leave without saving?");
        cut.Find("#confirm-modal-message").TextContent.Should().Contain("Unsaved changes will be lost.");

        // Act
        await cut.Find("#confirm-modal-cancel").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result.Should().NotBeNull();
        result!.Canceled.Should().BeTrue();
        cut.FindAll("#confirm-modal-confirm").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldConfirmAndCloseWithTrue()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, DefaultModel());

        // Act
        await cut.Find("#confirm-modal-confirm").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result.Should().NotBeNull();
        result!.Canceled.Should().BeFalse();
        result.Data.Should().Be(true);
    }

    [Fact]
    public async Task ItShouldUseTheDestructiveConfirmAction()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, DefaultModel(isDestructive: true));

        // Assert
        cut.Find("#confirm-modal-confirm").ClassList.Should().Contain("mud-button-filled-error");
        cut.Find("#confirm-modal-confirm").TextContent.Should().Contain("Leave");
        cut.Find("#confirm-modal-message").TextContent.Should().Contain("Unsaved changes will be lost.");
    }
}
