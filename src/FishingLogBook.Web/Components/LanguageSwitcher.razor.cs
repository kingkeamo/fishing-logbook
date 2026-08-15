using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Components;

public partial class LanguageSwitcher : ComponentBase
{
    [Inject]
    private ICultureService CultureService { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private Task SetCultureAsync(string cultureName)
    {
        return CultureService.SetCultureAsync(cultureName);
    }
}
