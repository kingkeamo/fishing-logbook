using System.Security.Claims;
using FishingLogBook.Web.Features.Authentication.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Authentication.Components.UserMenu;

public partial class UserMenu : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private ProfileSummaryModel _summary = ProfileSummaryModel.Empty;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private ISignedInUserDisplayService SignedInUserDisplay { get; set; } = default!;

    [Inject]
    private IProfileSummaryProvider ProfileSummary { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    protected override void OnInitialized()
    {
        ProfileSummary.Changed += OnSummaryChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await LoadSummaryAsync();
    }

    private void OnSummaryChanged()
    {
        _ = InvokeAsync(LoadSummaryAsync);
    }

    private async Task LoadSummaryAsync()
    {
        if (!await IsAuthenticatedAsync())
        {
            return;
        }

        var cancellationToken = _cancellationTokenSource.Token;
        ProfileSummaryModel summary;
        try
        {
            summary = await ProfileSummary.GetAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || summary == _summary)
        {
            return;
        }

        _summary = summary;
        await InvokeAsync(StateHasChanged);
    }

    private async Task<bool> IsAuthenticatedAsync()
    {
        if (AuthenticationStateTask is null)
        {
            return false;
        }

        var state = await AuthenticationStateTask;
        return state.User.Identity?.IsAuthenticated == true;
    }

    private void BeginSignIn()
    {
        Navigation.NavigateToLogin("authentication/login");
    }

    private void BeginSignOut()
    {
        ProfileSummary.Invalidate();
        Navigation.NavigateToLogout("authentication/logout");
    }

    private string SignedInLabel(ClaimsPrincipal user)
    {
        if (!string.IsNullOrWhiteSpace(_summary.DisplayName))
        {
            return _summary.DisplayName;
        }

        return SignedInUserDisplay.GetEmail(user) ?? Loc["Auth_UserMenu"];
    }

    public void Dispose()
    {
        ProfileSummary.Changed -= OnSummaryChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
