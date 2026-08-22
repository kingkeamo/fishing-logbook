using FishingLogBook.Web.Browser.Update;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Components.AppUpdateBanner;

public partial class AppUpdateBanner : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    [Inject]
    private IAppUpdateService AppUpdate { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool IsVisible => AppUpdate.Status != AppUpdateStatus.Current;

    private bool IsActivating => AppUpdate.Status == AppUpdateStatus.Activating;

    private bool ShowAction => AppUpdate.Status is AppUpdateStatus.Available or AppUpdateStatus.Failed;

    private string Title
    {
        get
        {
            return AppUpdate.Status switch
            {
                AppUpdateStatus.Activating => Loc["Update_ActivatingTitle"],
                AppUpdateStatus.Failed => Loc["Update_FailedTitle"],
                _ => Loc["Update_BannerTitle"]
            };
        }
    }

    private string Body
    {
        get
        {
            return AppUpdate.Status switch
            {
                AppUpdateStatus.Activating => Loc["Update_ActivatingBody"],
                AppUpdateStatus.Failed => Loc["Update_FailedBody"],
                _ => Loc["Update_BannerBody"]
            };
        }
    }

    protected override void OnInitialized()
    {
        AppUpdate.StatusChanged += OnStatusChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AppUpdate.StartAsync(_cancellationTokenSource.Token);
        }
    }

    public void Dispose()
    {
        AppUpdate.StatusChanged -= OnStatusChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private async Task UpdateAsync()
    {
        await AppUpdate.ApplyAsync(_cancellationTokenSource.Token);
    }

    private void OnStatusChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }
}
