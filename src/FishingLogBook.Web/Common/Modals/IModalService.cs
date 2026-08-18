using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Common.Modals;

public interface IModalService
{
    Task<bool> ConfirmAsync(ConfirmModalModel model, CancellationToken cancellationToken = default);

    Task ShowMessageAsync(MessageModalModel model, CancellationToken cancellationToken = default);

    Task<TResult?> ShowAsync<TModal, TModel, TResult>(
        TModel model,
        CancellationToken cancellationToken = default)
        where TModal : IComponent;
}
