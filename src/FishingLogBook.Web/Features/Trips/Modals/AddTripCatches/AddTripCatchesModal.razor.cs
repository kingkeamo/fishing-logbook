using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Trips.Modals.AddTripCatches;

public partial class AddTripCatchesModal : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private IReadOnlyList<CatchModel> _candidates = [];
    private IReadOnlyList<Guid> _selected = [];
    private string? _saveFailedMessage;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _loadFailed;
    private bool _rejected;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public AddTripCatchesModalModel Model { get; set; } = default!;

    [Inject]
    private ITripCatchService TripCatches { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string ConfirmLabel => _selected.Count == 1
        ? Loc["Trip_AddCatchesConfirmOne"]
        : Loc["Trip_AddCatchesConfirmMany", _selected.Count];

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        try
        {
            _candidates = await TripCatches.GetEligibleAsync(
                Model.Scope,
                Model.Storage,
                _cancellationTokenSource.Token);
            _selected = [];
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _candidates = [];
            _loadFailed = true;
            await Logging.LogErrorAsync(
                "reading the catches that can join a trip",
                exception,
                CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnSelectionChanged(IReadOnlyList<Guid> selected)
    {
        _selected = selected;
        _saveFailedMessage = null;
        _rejected = false;
    }

    private async Task SaveAsync()
    {
        if (_isSaving || _selected.Count == 0)
        {
            return;
        }

        _isSaving = true;
        _saveFailedMessage = null;
        _rejected = false;
        try
        {
            var association = await TripCatches.AssociateAsync(
                Model.Scope,
                _selected,
                Model.Storage,
                _cancellationTokenSource.Token);
            if (association.AssociatedCatchIds.Count == 0)
            {
                _rejected = true;
                await LoadAsync();
                return;
            }

            MudDialog.Close(DialogResult.Ok(new AddTripCatchesModalResult(
                association.AssociatedCatchIds,
                association.RejectedCatchIds)));
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (HttpRequestException exception)
        {
            await FailAsync(exception, exception.StatusCode is null);
        }
        catch (TaskCanceledException exception)
        {
            await FailAsync(exception, unreachable: true);
        }
        catch (Exception exception)
        {
            await FailAsync(exception, unreachable: false);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task FailAsync(Exception exception, bool unreachable)
    {
        await Logging.LogErrorAsync("adding catches to a trip", exception, CancellationToken.None);
        _saveFailedMessage = unreachable && Model.Storage == TripStorageEnum.Server
            ? Loc["Trip_AddCatchesOnlineRequired"].Value
            : Loc["Trip_AddCatchesFailed"].Value;
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
