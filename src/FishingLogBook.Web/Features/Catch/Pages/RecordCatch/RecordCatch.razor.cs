using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.Catch.Pages.RecordCatch;

public partial class RecordCatch : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Guid _ownerUserId;
    private AnglerPreferencesModel _preferences = AnglerPreferencesModel.Empty;
    private bool _isLoading = true;

    [Inject] private ILocalCatchOwnerService LocalCatchOwner { get; set; } = default!;
    [Inject] private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;
    [Inject] private ICatchSynchroniser CatchSynchroniser { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            try
            {
                _ownerUserId = await LocalCatchOwner.GetUserIdAsync(_cancellationTokenSource.Token);
            }
            catch (Exception)
            {
                _ownerUserId = Guid.Empty;
            }

            _preferences = await AnglerPreferences.GetAsync(_cancellationTokenSource.Token);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private Task OnSavedAsync()
    {
        _ = SynchroniseAsync();
        return Task.CompletedTask;
    }

    private async Task SynchroniseAsync()
    {
        try
        {
            await CatchSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("catch synchronisation", exception, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
