using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Common.Modals.AnglerPicker;

public partial class AnglerPickerModal : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private CancellationTokenSource? _searchCancellationTokenSource;
    private IReadOnlyList<AnglerSummaryDto> _results = [];
    private string _query = string.Empty;
    private string? _failedMessage;
    private int _searchGeneration;
    private bool _isSearching;
    private bool _hasSearched;

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public AnglerPickerModalModel Model { get; set; } = new();
    [Inject] private IProfileClient ProfileClient { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string DisplayName(AnglerSummaryDto angler)
    {
        if (!string.IsNullOrWhiteSpace(angler.DisplayName))
        {
            return angler.DisplayName;
        }

        return string.IsNullOrWhiteSpace(angler.Email) ? Loc["Trip_ContributorUnknown"].Value : angler.Email;
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
        _isSearching = true;
        try
        {
            var results = await ProfileClient.FindAnglersAsync(_query.Trim(), searchCts.Token);
            if (generation == _searchGeneration)
            {
                _results = results.Where(angler => !Model.ExcludedUserIds.Contains(angler.UserId)).ToArray();
                _hasSearched = true;
            }
        }
        catch (OperationCanceledException) when (searchCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (generation == _searchGeneration)
            {
                _results = [];
                _hasSearched = true;
                _failedMessage = Loc["Trip_InviteSearchFailed"].Value;
                await Logging.LogErrorAsync("searching for anglers", exception, CancellationToken.None);
            }
        }
        finally
        {
            if (generation == _searchGeneration)
            {
                _isSearching = false;
            }
        }
    }

    private void Select(AnglerSummaryDto angler)
    {
        MudDialog.Close(DialogResult.Ok(new AnglerPickerModalResult(angler)));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private void CancelActiveSearch()
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();
        _searchCancellationTokenSource = null;
    }

    public void Dispose()
    {
        CancelActiveSearch();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
