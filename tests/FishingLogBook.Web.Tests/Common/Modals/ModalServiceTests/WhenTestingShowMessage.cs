using FishingLogBook.Web.Common.Modals;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Modals.ModalServiceTests;

public class WhenTestingShowMessage : BaseModalServiceTest
{
    [Fact]
    public async Task ItShouldOpenAndWaitForTheMessageModalToClose()
    {
        // Arrange
        var model = new MessageModalModel(
            "Network unavailable",
            "The catch is still saved on this device.",
            "Close",
            ModalSeverity.Information);
        var dialog = DialogThatReturns(DialogResult.Ok(true));
        var (service, dialogs) = CreateService();
        dialogs.ShowAsync<MessageModal>(
                Arg.Is<string>(title => title == model.Title),
                Arg.Is<DialogParameters<MessageModal>>(parameters =>
                    parameters.Get(modal => modal.Model) == model),
                Arg.Is<DialogOptions>(options =>
                    options.CloseButton == true && options.CloseOnEscapeKey == true))
            .Returns(dialog);

        // Act
        await service.ShowMessageAsync(model);

        // Assert
        await dialogs.Received(1).ShowAsync<MessageModal>(
            model.Title,
            Arg.Is<DialogParameters<MessageModal>>(parameters =>
                parameters.Get(modal => modal.Model) == model),
            Arg.Is<DialogOptions>(options =>
                options.CloseButton == true && options.CloseOnEscapeKey == true));
        _ = dialog.Received(1).Result;
    }
}
