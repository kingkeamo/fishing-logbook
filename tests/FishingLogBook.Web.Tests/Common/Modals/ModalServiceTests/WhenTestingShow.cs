using AwesomeAssertions;
using FishingLogBook.Web.Common.Modals;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Modals.ModalServiceTests;

public class WhenTestingShow : BaseModalServiceTest
{
    [Fact]
    public async Task ItShouldReturnDefaultWhenTheModalIsCancelled()
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
                Arg.Is<DialogParameters>(parameters =>
                    parameters.Get<ConfirmModalModel>("Model") == model),
                Arg.Is<DialogOptions>(options =>
                    options.CloseButton == true && options.CloseOnEscapeKey == true))
            .Returns(dialog);

        // Act
        var result = await service.ShowAsync<ConfirmModal, ConfirmModalModel, string>(model);

        // Assert
        result.Should().BeNull();
        await dialogs.Received(1).ShowAsync<ConfirmModal>(
            Arg.Is<DialogParameters>(parameters =>
                parameters.Get<ConfirmModalModel>("Model") == model),
            Arg.Is<DialogOptions>(options =>
                options.CloseButton == true && options.CloseOnEscapeKey == true));
    }

    [Fact]
    public async Task ItShouldReturnTheTypedResultWhenTheModalCompletes()
    {
        // Arrange
        var model = new ConfirmModalModel(
            "Leave without saving?",
            "Unsaved changes will be lost.",
            "Leave",
            "Stay");
        var dialog = DialogThatReturns(DialogResult.Ok("saved"));
        var (service, dialogs) = CreateService();
        dialogs.ShowAsync<ConfirmModal>(
                Arg.Is<DialogParameters>(parameters =>
                    parameters.Get<ConfirmModalModel>("Model") == model),
                Arg.Is<DialogOptions>(options =>
                    options.CloseButton == true && options.CloseOnEscapeKey == true))
            .Returns(dialog);

        // Act
        var result = await service.ShowAsync<ConfirmModal, ConfirmModalModel, string>(model);

        // Assert
        result.Should().Be("saved");
        await dialogs.Received(1).ShowAsync<ConfirmModal>(
            Arg.Is<DialogParameters>(parameters =>
                parameters.Get<ConfirmModalModel>("Model") == model),
            Arg.Is<DialogOptions>(options =>
                options.CloseButton == true && options.CloseOnEscapeKey == true));
    }
}
