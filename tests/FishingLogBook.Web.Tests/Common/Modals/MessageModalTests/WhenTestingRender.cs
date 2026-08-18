using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common.Modals;

namespace FishingLogBook.Web.Tests.Common.Modals.MessageModalTests;

public class WhenTestingRender : BaseMessageModalTest
{
    [Fact]
    public async Task ItShouldCloseThroughTheDialogInstance()
    {
        // Arrange
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(
            context,
            new MessageModalModel(
                "Network unavailable",
                "The catch is still saved on this device.",
                "Close"));

        // Act
        cut.Find("#message-modal-title").TextContent.Should().Contain("Network unavailable");
        cut.Find("#message-modal-message").TextContent.Should().Contain("The catch is still saved on this device.");
        await cut.Find("#message-modal-close").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result.Should().NotBeNull();
        result!.Canceled.Should().BeFalse();
        cut.FindAll("#message-modal-message").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderTheWarningSeverityWithoutMudEnumsInTheModel()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var (cut, dialog) = await ShowAsync(
            context,
            new MessageModalModel(
                "Public location",
                "The exact fishing spot may be visible.",
                "Close",
                ModalSeverity.Warning));

        // Assert
        cut.Find("#message-modal-message").ClassList.Should().Contain("mud-alert-text-warning");
        dialog.Result.IsCompleted.Should().BeFalse();
    }
}
