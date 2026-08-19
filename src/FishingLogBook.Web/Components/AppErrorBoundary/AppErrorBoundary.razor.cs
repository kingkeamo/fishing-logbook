using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Components.AppErrorBoundary;

public partial class AppErrorBoundary : ComponentBase, IDisposable
{
    private LoggingErrorBoundary? _errorBoundary;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    private void TryAgain()
    {
        _errorBoundary?.Recover();
    }

    private void GoHome()
    {
        _errorBoundary?.Recover();
        NavigationManager.NavigateTo("/");
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        _errorBoundary?.Recover();
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}
