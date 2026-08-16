using System.Globalization;
using FishingLogBook.Web.Features.Diagnostics.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Diagnostics.Storage;
using Microsoft.JSInterop;

namespace FishingLogBook.Web.Localization;

public sealed class CultureService : ICultureService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILoggingService _logging;

    public CultureService(IJSRuntime jsRuntime, ILoggingService logging)
    {
        _jsRuntime = jsRuntime;
        _logging = logging;
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
        catch (JSException exception)
        {
            await _logging.LogErrorAsync("culture read", exception);
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
