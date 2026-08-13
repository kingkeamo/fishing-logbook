using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Localization;

public sealed class FishingLogBookMudLocalizer : MudLocalizer
{
    private readonly IStringLocalizer<UiStrings> _localizer;

    public FishingLogBookMudLocalizer(IStringLocalizer<UiStrings> localizer)
    {
        _localizer = localizer;
    }

    public override LocalizedString this[string key] => _localizer[key];

    public override LocalizedString this[string key, params object[] arguments] => _localizer[key, arguments];
}
