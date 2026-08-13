using System.Globalization;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Localization;

public sealed class CultureService : ICultureService
{
    private readonly IJSRuntime _jsRuntime;

    public CultureService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public string CurrentCulture => CultureInfo.CurrentUICulture.Name;

    public async Task InitializeAsync()
    {
        string? stored = null;
        string? browser = null;
        try
        {
            stored = await _jsRuntime.InvokeAsync<string?>("fishingLogBookCulture.get");
            browser = await _jsRuntime.InvokeAsync<string?>("fishingLogBookCulture.browser");
        }
        catch (JSException)
        {
        }

        Apply(CultureMatcher.Resolve(stored, browser));
    }

    public async Task SetCultureAsync(string cultureName)
    {
        var resolved = CultureMatcher.Resolve(cultureName, null);
        await _jsRuntime.InvokeVoidAsync("fishingLogBookCulture.set", resolved);
        await _jsRuntime.InvokeVoidAsync("fishingLogBookCulture.reload");
    }

    private static void Apply(string cultureName)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.GetCultureInfo(CultureNames.English);
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
