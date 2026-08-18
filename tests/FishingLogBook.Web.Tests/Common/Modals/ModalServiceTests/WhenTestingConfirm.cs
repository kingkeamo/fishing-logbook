using AwesomeAssertions;
using FishingLogBook.Web.Common.Modals;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Modals.ModalServiceTests;

public class WhenTestingConfirm : BaseModalServiceTest
{
    [Fact]
    public async Task ItShouldReturnFalseWhenTheConfirmModalIsCancelled()
    {
        // Arrange
        var model = new ConfirmModalModel(
            "Leave without saving?",
            "Unsaved changes will be lost.",
            "Leave",
            "Stay");
        var dialog = DialogThatReturns(DialogResult.Cancel());
        var (service, dialogs) = CreateService();
        dialogs.ShowAsync<ConfirmModal>(
                Arg.Is<string>(title => title == model.Title),
                Arg.Is<DialogParameters<ConfirmModal>>(parameters =>
                    parameters.Get(modal => modal.Model) == model),
                Arg.Is<DialogOptions>(options =>
                    options.CloseButton == true && options.CloseOnEscapeKey == true))
            .Returns(dialog);

        // Act
        var confirmed = await service.ConfirmAsync(model);

        // Assert
        confirmed.Should().BeFalse();
        await dialogs.Received(1).ShowAsync<ConfirmModal>(
            model.Title,
            Arg.Is<DialogParameters<ConfirmModal>>(parameters =>
                parameters.Get(modal => modal.Model) == model),
            Arg.Is<DialogOptions>(options =>
                options.CloseButton == true && options.CloseOnEscapeKey == true));
    }

    [Fact]
    public async Task ItShouldReturnTrueWhenTheConfirmModalIsConfirmed()
    {
        // Arrange
        var model = new ConfirmModalModel(
            "Leave without saving?",
            "Unsaved changes will be lost.",
            "Leave",
            "Stay");
        var dialog = DialogThatReturns(DialogResult.Ok(true));
        var (service, dialogs) = CreateService();
        dialogs.ShowAsync<ConfirmModal>(
                Arg.Is<string>(title => title == model.Title),
                Arg.Is<DialogParameters<ConfirmModal>>(parameters =>
                    parameters.Get(modal => modal.Model) == model),
                Arg.Is<DialogOptions>(options =>
                    options.CloseButton == true && options.CloseOnEscapeKey == true))
            .Returns(dialog);

        // Act
        var confirmed = await service.ConfirmAsync(model);

        // Assert
        confirmed.Should().BeTrue();
        await dialogs.Received(1).ShowAsync<ConfirmModal>(
            model.Title,
            Arg.Is<DialogParameters<ConfirmModal>>(parameters =>
                parameters.Get(modal => modal.Model) == model),
            Arg.Is<DialogOptions>(options =>
                options.CloseButton == true && options.CloseOnEscapeKey == true));
    }
}
