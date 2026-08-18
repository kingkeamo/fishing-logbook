using FishingLogBook.Web.Common.Modals;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Modals.ModalServiceTests;

public class BaseModalServiceTest
{
    protected static (ModalService Service, IDialogService Dialogs) CreateService()
    {
        var dialogs = Substitute.For<IDialogService>();
        return (new ModalService(dialogs), dialogs);
    }

    protected static IDialogReference DialogThatReturns(DialogResult result)
    {
        var dialog = Substitute.For<IDialogReference>();
        dialog.Result.Returns(Task.FromResult<DialogResult?>(result));
        return dialog;
    }
}
