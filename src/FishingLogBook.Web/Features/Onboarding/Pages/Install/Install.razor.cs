using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Onboarding.Pages.Install;

public partial class Install : ComponentBase
{
    private bool _isLoading = true;
    private InstallState _state = new(false, false, false, false);

    [Inject] private IInstallService InstallService { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task InstallAsync()
    {
        await InstallService.PromptAsync(CancellationToken.None);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            _state = await InstallService.GetStateAsync(CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
        }
    }
}
