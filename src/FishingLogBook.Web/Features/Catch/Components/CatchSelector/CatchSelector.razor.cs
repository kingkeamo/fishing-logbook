using System.Globalization;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.Catch.Components.CatchSelector;

public partial class CatchSelector : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly HashSet<Guid> _selected = [];
    private readonly Dictionary<Guid, string> _mediaSources = [];
    private readonly HashSet<Guid> _mediaRequested = [];

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<CatchModel> Catches { get; set; } = [];

    [Parameter]
    [EditorRequired]
    public string ConfirmLabel { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public string EmptyLabel { get; set; } = string.Empty;

    [Parameter]
    public string UnknownSpeciesLabel { get; set; } = string.Empty;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public Guid OwnerUserId { get; set; }

    [Parameter]
    public EventCallback<IReadOnlyList<Guid>> OnConfirm { get; set; }

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        var available = Catches.Select(candidate => candidate.Id).ToHashSet();
        _selected.RemoveWhere(id => !available.Contains(id));
        await LoadThumbnailsAsync();
    }

    private async Task LoadThumbnailsAsync()
    {
        foreach (var candidate in Catches)
        {
            var photograph = candidate.Photographs.Count > 0 ? candidate.Photographs[0] : null;
            if (photograph is null || !_mediaRequested.Add(photograph.Id))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(photograph.RemoteUrl))
            {
                _mediaSources[photograph.Id] = photograph.RemoteUrl!;
                continue;
            }

            await LoadThumbnailAsync(candidate.Id, photograph);
        }
    }

    private async Task LoadThumbnailAsync(Guid catchId, CatchPhotographModel photograph)
    {
        try
        {
            var bytes = await CatchStore.GetPhotographBytesAsync(
                OwnerUserId,
                catchId,
                photograph.Id,
                _cancellationTokenSource.Token);
            if (bytes is { Length: > 0 })
            {
                _mediaSources[photograph.Id] =
                    $"data:{photograph.ContentType};base64,{Convert.ToBase64String(bytes)}";
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading a catch selector thumbnail", exception, CancellationToken.None);
        }
    }

    private string? MediaSource(CatchModel candidate)
    {
        var photograph = candidate.Photographs.Count > 0 ? candidate.Photographs[0] : null;
        return photograph is not null && _mediaSources.TryGetValue(photograph.Id, out var source)
            ? source
            : null;
    }

    private bool IsSelected(Guid catchId)
    {
        return _selected.Contains(catchId);
    }

    private Task ToggleAsync(Guid catchId, bool selected)
    {
        if (selected)
        {
            _selected.Add(catchId);
        }
        else
        {
            _selected.Remove(catchId);
        }

        return Task.CompletedTask;
    }

    private async Task ConfirmAsync()
    {
        if (_selected.Count == 0 || !OnConfirm.HasDelegate)
        {
            return;
        }

        var chosen = Catches
            .Where(candidate => _selected.Contains(candidate.Id))
            .Select(candidate => candidate.Id)
            .ToArray();
        await OnConfirm.InvokeAsync(chosen);
    }

    private string Describe(CatchModel candidate)
    {
        var species = string.IsNullOrWhiteSpace(candidate.SpeciesName)
            ? UnknownSpeciesLabel
            : candidate.SpeciesName!;
        var caughtOn = candidate.CaughtOn.ToString("d MMM HH:mm", CultureInfo.CurrentCulture);
        return $"{species} · {caughtOn}";
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
