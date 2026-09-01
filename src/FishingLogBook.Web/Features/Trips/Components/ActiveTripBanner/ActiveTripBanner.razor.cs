using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.ActiveTripBanner;

public partial class ActiveTripBanner : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Guid _viewerUserId;

    private TripModel? _trip;

    [Inject]
    private IActiveTripService ActiveTrip { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalOwner { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Parameter]
    public Guid ViewerUserId { get; set; }

    [Parameter]
    public string TripBaseHref { get; set; } = "/trips";

    private string ViewHref
    {
        get
        {
            return TripHref(string.Empty);
        }
    }

    private bool CanUpdate
    {
        get
        {
            return _trip?.IsOwnedBy(_viewerUserId) == true;
        }
    }

    private string EditHref
    {
        get
        {
            return TripHref("/edit");
        }
    }

    private string TripHref(string suffix)
    {
        if (_trip is null)
        {
            return "/catches";
        }

        var root = string.IsNullOrWhiteSpace(TripBaseHref) ? "/trips" : TripBaseHref.TrimEnd('/');
        return $"{root}/{_trip.Id:D}{suffix}";
    }

    protected override async Task OnInitializedAsync()
    {
        ActiveTrip.StateChanged += OnActiveTripChanged;
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            _viewerUserId = ViewerUserId != Guid.Empty
                ? ViewerUserId
                : await LocalOwner.GetUserIdAsync(cancellationToken);
            _trip = await ActiveTrip.GetActiveAsync(_viewerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _trip = null;
            await Logging.LogErrorAsync("resolving the active trip banner", exception, CancellationToken.None);
        }
    }

    private void OnActiveTripChanged(object? sender, EventArgs args)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _ = InvokeAsync(async () =>
            {
                await RefreshAsync();
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        ActiveTrip.StateChanged -= OnActiveTripChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
