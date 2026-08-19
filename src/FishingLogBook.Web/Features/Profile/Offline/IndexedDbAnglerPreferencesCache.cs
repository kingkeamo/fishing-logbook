using System.Text.Json;
using FishingLogBook.Web.Features.Profile.Models;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Features.Profile.Offline;

public sealed class IndexedDbAnglerPreferencesCache : IAnglerPreferencesCache
{
    private const string ModulePath = "./js/preference-store.js";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _jsRuntime;
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public IndexedDbAnglerPreferencesCache(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<AnglerPreferencesModel?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var module = await GetModuleAsync(cancellationToken);
        var json = await module.InvokeAsync<string?>(
            "getFishingPreferences",
            cancellationToken,
            userId.ToString("D"));
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AnglerPreferencesModel>(json, SerializerOptions);
    }

    public async Task SaveAsync(
        Guid userId,
        AnglerPreferencesModel preferences,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var module = await GetModuleAsync(cancellationToken);
        await module.InvokeVoidAsync(
            "putFishingPreferences",
            cancellationToken,
            userId.ToString("D"),
            JsonSerializer.Serialize(preferences, SerializerOptions));
    }

    private async Task<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return _module;
        }

        await _moduleLock.WaitAsync(cancellationToken);
        try
        {
            _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                cancellationToken,
                ModulePath);
            return _module;
        }
        finally
        {
            _moduleLock.Release();
        }
    }
}
