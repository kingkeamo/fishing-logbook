using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.RecordCatch;

public partial class RecordCatch : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Guid _ownerUserId;
    private AnglerPreferencesModel _preferences = AnglerPreferencesModel.Empty;
    private IReadOnlyList<CatchAnglerOptionModel> _anglerOptions = [];
    private bool _isLoading = true;
    private bool _ownerResolutionFailed;

    [SupplyParameterFromQuery(Name = "tripId")]
    [Parameter]
    public Guid? TripId { get; set; }

    [Inject] private ILocalCatchOwnerService LocalCatchOwner { get; set; } = default!;
    [Inject] private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;
    [Inject] private ILogbookSynchroniser LogbookSynchroniser { get; set; } = default!;
    [Inject] private ITripParticipantClient ParticipantClient { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _ownerUserId = await LocalCatchOwner.GetUserIdAsync(_cancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("resolving the catch owner", exception, CancellationToken.None);
            _ownerResolutionFailed = true;
            _isLoading = false;
            return;
        }

        try
        {
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

    private async Task OnTripAssociatedAsync(Guid tripId)
    {
        _anglerOptions = [];
        try
        {
            var participants = await ParticipantClient.GetAsync(tripId, _cancellationTokenSource.Token);
            if (participants is null)
            {
                return;
            }

            _anglerOptions =
            [
                .. participants.Participants
                    .Where(participant =>
                        participant.IsOwner || participant.Status == TripParticipantConstants.Accepted)
                    .Select(participant => new CatchAnglerOptionModel(
                        participant.UserId,
                        participant.UserId == _ownerUserId
                            ? Loc["Catch_AnglerMe"].Value
                            : (string.IsNullOrWhiteSpace(participant.DisplayName)
                                ? Loc["Trip_ContributorUnknown"].Value
                                : participant.DisplayName)))
            ];
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading trip anglers", exception, CancellationToken.None);
        }
    }

    private async Task SynchroniseAsync()
    {
        try
        {
            await LogbookSynchroniser.SynchronisePendingAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("logbook synchronisation", exception, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
