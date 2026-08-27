using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.TripPhotographs;

public partial class TripPhotographs : ComponentBase, IDisposable
{
    private const int MaxPhotographs = 10;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<TripPhotographModel> _photographs = [];
    private Guid? _activePhotographId;
    private bool _addFailed;
    private bool _removeFailed;

    [Parameter]
    [EditorRequired]
    public TripModel Trip { get; set; } = default!;

    [Parameter]
    public EventCallback Changed { get; set; }

    [Inject]
    private ITripPhotographStore PhotographStore { get; set; } = default!;

    [Inject]
    private ITripClient TripClient { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IReadOnlyList<PhotographCarouselItemModel> CarouselPhotographs =>
        [.. _photographs
            .Where(photograph => photograph.Bytes is { Length: > 0 })
            .Select(photograph => new PhotographCarouselItemModel(
                photograph.Id,
                photograph.ContentType,
                photograph.Bytes!))];

    protected override async Task OnInitializedAsync()
    {
        await LoadStoredPhotographsAsync();
    }

    private async Task LoadStoredPhotographsAsync()
    {
        var stored = Trip.Photographs ?? [];
        foreach (var photograph in stored.OrderBy(photograph => photograph.OrderedOn))
        {
            try
            {
                var bytes = await PhotographStore.GetBytesAsync(
                    Trip.OwnerUserId,
                    Trip.Id,
                    photograph.Id,
                    _cancellationTokenSource.Token);
                if (bytes is { Length: > 0 })
                {
                    _photographs.Add(photograph with { Bytes = bytes });
                }
            }
            catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                await Logging.LogErrorAsync(
                    "loading a stored trip photograph",
                    exception,
                    CancellationToken.None);
            }
        }

        _activePhotographId ??= _photographs.FirstOrDefault()?.Id;
    }

    private async Task OnPhotographsPreparedAsync(IReadOnlyList<PreparedPhotographModel> prepared)
    {
        _addFailed = false;
        _removeFailed = false;
        foreach (var photograph in prepared)
        {
            if (_photographs.Count >= MaxPhotographs)
            {
                break;
            }

            await AddPhotographAsync(photograph);
        }

        await Changed.InvokeAsync();
    }

    private async Task AddPhotographAsync(PreparedPhotographModel prepared)
    {
        var model = new TripPhotographModel(
            prepared.Id,
            Trip.Id,
            Trip.OwnerUserId,
            prepared.ContentType,
            DateTimeOffset.UtcNow,
            prepared.Metadata.HasTrustworthyCapturedOn ? prepared.Metadata.CapturedOn : null,
            prepared.Bytes);
        try
        {
            await PhotographStore.SaveAsync(model, _cancellationTokenSource.Token);
            _photographs.Add(model);
            _activePhotographId ??= model.Id;
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("adding a trip photograph", exception, CancellationToken.None);
            _addFailed = true;
        }
    }

    private async Task RemovePhotographAsync(Guid photographId)
    {
        _removeFailed = false;
        var photograph = _photographs.FirstOrDefault(item => item.Id == photographId);
        if (photograph is null)
        {
            return;
        }

        try
        {
            if (photograph.SyncStatus == SyncStatus.Synchronised)
            {
                await TripClient.DeletePhotographAsync(
                    Trip.Id,
                    photographId,
                    _cancellationTokenSource.Token);
            }

            await PhotographStore.DeleteAsync(
                Trip.OwnerUserId,
                Trip.Id,
                photographId,
                _cancellationTokenSource.Token);
            _photographs.Remove(photograph);
            if (_activePhotographId == photographId)
            {
                _activePhotographId = _photographs.FirstOrDefault()?.Id;
            }

            await Changed.InvokeAsync();
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("removing a trip photograph", exception, CancellationToken.None);
            _removeFailed = true;
        }
    }

    private void OnActivePhotographChanged(Guid? photographId)
    {
        _activePhotographId = photographId;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
