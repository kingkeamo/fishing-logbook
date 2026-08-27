using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FishingLogBook.Web.Common.Modals;

public sealed class ModalService : IModalService
{
    private readonly IDialogService _dialogService;

    public ModalService(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task<bool> ConfirmAsync(
        ConfirmModalModel model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parameters = new DialogParameters<ConfirmModal>
        {
            { modal => modal.Model, model }
        };
        var dialog = await _dialogService.ShowAsync<ConfirmModal>(
            model.Title,
            parameters,
            CreateCompactOptions());
        var result = await dialog.Result;
        return result is { Canceled: false, Data: true };
    }

    public async Task ShowMessageAsync(
        MessageModalModel model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parameters = new DialogParameters<MessageModal>
        {
            { modal => modal.Model, model }
        };
        var dialog = await _dialogService.ShowAsync<MessageModal>(
            model.Title,
            parameters,
            CreateCompactOptions());
        await dialog.Result;
    }

    public async Task<TResult?> ShowAsync<TModal, TModel, TResult>(
        TModel model,
        CancellationToken cancellationToken = default)
        where TModal : IComponent
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parameters = new DialogParameters
        {
            { "Model", model }
        };
        var dialog = await _dialogService.ShowAsync<TModal>(
            parameters,
            CreateOptions());
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not TResult typed)
        {
            return default;
        }

        return typed;
    }

    private static DialogOptions CreateOptions()
    {
        return new DialogOptions
        {
            CloseButton = true,
            CloseOnEscapeKey = true,
            FullWidth = true,
            MaxWidth = MaxWidth.Small
        };
    }

    private static DialogOptions CreateCompactOptions()
    {
        return new DialogOptions
        {
            CloseButton = true,
            CloseOnEscapeKey = true,
            FullWidth = false,
            MaxWidth = MaxWidth.Small
        };
    }
}
