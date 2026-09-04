using FishingLogBook.Web.Features.Import.Models;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Import.Services;

public sealed class ImportPhotoBlobRegistryService : IImportPhotoBlobRegistryService, IAsyncDisposable
{
    private const string ModulePath = "./js/import/photo-blob-registry.js";
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public ImportPhotoBlobRegistryService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<ImportPhotoBlobRegistrationModel> RegisterAsync(
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<ImportPhotoBlobRegistrationModel>(
            "register",
            CancellationToken.None,
            bytes,
            contentType);
    }

    public async Task<byte[]> GetBytesAsync(string token, CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<byte[]>("getBytes", cancellationToken, token);
    }

    public async Task RemoveAsync(string token, CancellationToken cancellationToken)
    {
        var module = await GetModuleAsync(cancellationToken);
        await module.InvokeVoidAsync("remove", cancellationToken, token);
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("clear", cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("clear");
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        return _module;
    }
}
