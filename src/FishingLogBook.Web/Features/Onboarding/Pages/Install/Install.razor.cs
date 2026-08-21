using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Onboarding.Pages.Install;

public partial class Install : ComponentBase
{
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
}
