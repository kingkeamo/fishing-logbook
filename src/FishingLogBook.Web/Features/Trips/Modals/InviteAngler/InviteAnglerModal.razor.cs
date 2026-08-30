using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Trips.Modals.InviteAngler;

public partial class InviteAnglerModal : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private CancellationTokenSource? _searchCancellationTokenSource;
    private int _searchGeneration;
    private IReadOnlyList<AnglerSummaryDto> _results = [];
    private string _query = string.Empty;
    private string? _failedMessage;
    private bool _isSearching;
    private bool _isInviting;
    private bool _hasSearched;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public InviteAnglerModalModel Model { get; set; } = default!;

    [Inject]
    private IProfileClient ProfileClient { get; set; } = default!;

    [Inject]
    private ITripParticipantClient ParticipantClient { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string DisplayName(AnglerSummaryDto angler)
    {
        if (!string.IsNullOrWhiteSpace(angler.DisplayName))
        {
            return angler.DisplayName;
        }

        return string.IsNullOrWhiteSpace(angler.Email)
            ? Loc["Trip_ContributorUnknown"].Value
            : angler.Email;
    }

    private async Task OnQueryChangedAsync(string value)
    {
        _query = value;
        _failedMessage = null;
        if (!AnglerLookupConstants.IsQueryValid(_query))
        {
            CancelActiveSearch();
            _results = [];
            _hasSearched = false;
            _isSearching = false;
            return;
        }

        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        CancelActiveSearch();
        var searchCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
        _searchCancellationTokenSource = searchCts;
        var generation = ++_searchGeneration;
        var query = _query.Trim();

        _isSearching = true;
        try
        {
            var results = await ProfileClient.FindAnglersAsync(query, searchCts.Token);
            if (generation != _searchGeneration)
            {
                return;
            }

            _results = results;
            _hasSearched = true;
        }
        catch (OperationCanceledException) when (searchCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (generation != _searchGeneration)
            {
                return;
            }

            _results = [];
            _hasSearched = true;
            _failedMessage = Loc["Trip_InviteSearchFailed"].Value;
            await Logging.LogErrorAsync("searching for anglers", exception, CancellationToken.None);
        }
        finally
        {
            if (generation == _searchGeneration)
            {
                _isSearching = false;
            }
        }
    }

    private void CancelActiveSearch()
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();
        _searchCancellationTokenSource = null;
    }

    private async Task InviteAsync(Guid userId)
    {
        if (_isInviting)
        {
            return;
        }

        _isInviting = true;
        _failedMessage = null;
        try
        {
            var participants = await ParticipantClient.InviteAsync(
                Model.TripId,
                new InviteTripParticipantDto(userId),
                _cancellationTokenSource.Token);
            if (participants is null)
            {
                _failedMessage = Loc["Trip_InviteFailed"].Value;
                return;
            }

            MudDialog.Close(DialogResult.Ok(new InviteAnglerModalResult(participants)));
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _failedMessage = Loc["Trip_InviteFailed"].Value;
            await Logging.LogErrorAsync("inviting an angler to a trip", exception, CancellationToken.None);
        }
        finally
        {
            _isInviting = false;
        }
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    public void Dispose()
    {
        CancelActiveSearch();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
