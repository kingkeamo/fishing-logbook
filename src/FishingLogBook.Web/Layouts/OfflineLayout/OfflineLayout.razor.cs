using FishingLogBook.Web.Features.OfflineAccess.Enums;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Layouts.OfflineLayout;

public partial class OfflineLayout : LayoutComponentBase, IDisposable
{
    private readonly MudTheme _theme = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _drawerOpen = true;
    private bool _isDarkMode;

    [Inject] private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;
    [Inject] private IOfflineReconnectService OfflineReconnect { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        OfflineReconnect.StateChanged += OnReconnectStateChanged;
        await OfflineReconnect.StartAsync(_cancellationTokenSource.Token);
    }

    private void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
    }

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    private void Lock()
    {
        OfflineOwnerContext.Lock();
        Navigation.NavigateTo("/", replace: true);
    }

    private void OnReconnectStateChanged(object? sender, EventArgs args)
    {
        _ = InvokeAsync(() =>
        {
            if (OfflineReconnect.State == OfflineReconnectStateEnum.Online)
            {
                Navigation.NavigateTo("/catches", replace: true);
                return;
            }

            StateHasChanged();
        });
    }

    private Task RetryReconnectAsync()
    {
        return OfflineReconnect.AttemptAsync(_cancellationTokenSource.Token);
    }

    private void SignInToSynchronise()
    {
        Navigation.NavigateToLogin(
            "authentication/login",
            new InteractiveRequestOptions
            {
                Interaction = InteractionType.SignIn,
                ReturnUrl = "/?reconnect=offline"
            });
    }

    public void Dispose()
    {
        OfflineReconnect.StateChanged -= OnReconnectStateChanged;
        OfflineReconnect.Stop();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
