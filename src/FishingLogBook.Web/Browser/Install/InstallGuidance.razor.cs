using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Browser.Install;

public partial class InstallGuidance : ComponentBase
{
    private InstallState _state = new(false, false, false, false);
    private InstallResult _result = InstallResult.Unavailable;
    private bool _isLoading = true;
    private bool _isPrompting;

    [Parameter]
    public bool ShowInstallLaterMessage { get; set; }

    [Inject]
    private IInstallService InstallService { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task InstallAsync()
    {
        if (_isPrompting)
        {
            return;
        }

        _isPrompting = true;
        try
        {
            _result = await InstallService.PromptAsync(CancellationToken.None);
            if (_result == InstallResult.Accepted)
            {
                _state = _state with { IsInstalled = true, CanPrompt = false };
            }
            else
            {
                await RefreshAsync();
            }
        }
        finally
        {
            _isPrompting = false;
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            _state = await InstallService.GetStateAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            _state = new InstallState(false, false, InstallPlatformFamilies.Other, false);
        }
        finally
        {
            _isLoading = false;
        }
    }
}
