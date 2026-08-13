using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Localization;

public sealed class CultureService : ICultureService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigationManager;

    public CultureService(IJSRuntime jsRuntime, NavigationManager navigationManager)
    {
        _jsRuntime = jsRuntime;
        _navigationManager = navigationManager;
    }

    public string CurrentCulture => CultureInfo.CurrentUICulture.Name;

    public async Task InitializeAsync()
    {
        var stored = await _jsRuntime.InvokeAsync<string?>("fishingLogBookCulture.get");
        var browser = await _jsRuntime.InvokeAsync<string?>("fishingLogBookCulture.browser");
        var cultureName = CultureMatcher.Resolve(stored, browser);
        Apply(cultureName);
        await _jsRuntime.InvokeVoidAsync("fishingLogBookCulture.set", cultureName);
    }

    public async Task SetCultureAsync(string cultureName)
    {
        var resolved = CultureMatcher.Resolve(cultureName, null);
        await _jsRuntime.InvokeVoidAsync("fishingLogBookCulture.set", resolved);
        _navigationManager.NavigateTo(_navigationManager.Uri, forceLoad: true);
    }

    private static void Apply(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
