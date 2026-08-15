using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Pages;

public partial class NotFound : ComponentBase
{
    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
}
