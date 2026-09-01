using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.OfflineCatchEdit;

public partial class OfflineCatchEdit : ComponentBase
{
    private CatchModel? _catch;
    private AnglerPreferencesModel _preferences = AnglerPreferencesModel.Empty;
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _accessLocked;

    [Parameter]
    public Guid CatchId { get; set; }

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesStore AnglerPreferencesStore { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private IReadOnlyList<PhotographCarouselItemModel> CarouselPhotographs
    {
        get
        {
            return _catch?.Photographs
                .Select(photo => new PhotographCarouselItemModel(
                    photo.Id,
                    photo.ContentType,
                    photo.Bytes,
                    photo.RemoteUrl))
                .ToArray() ?? [];
        }
    }

    protected override Task OnParametersSetAsync()
    {
        return LoadAsync();
    }

    private async Task RetryLoadAsync()
    {
        if (_isLoading || _accessLocked)
        {
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        _accessLocked = false;
        _catch = null;
        try
        {
            var owner = OfflineOwnerContext.Owner;
            if (owner is null)
            {
                _accessLocked = true;
                return;
            }

            _catch = await CatchStore.GetAsync(owner.UserId, CatchId, CancellationToken.None);
            if (_catch is null || _catch.UserId != owner.UserId)
            {
                _loadFailed = true;
                return;
            }

            _preferences = await AnglerPreferencesStore.GetAsync(owner.UserId, CancellationToken.None)
                ?? AnglerPreferencesModel.Empty;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading a catch for offline editing", exception, CancellationToken.None);
            _loadFailed = true;
            _catch = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private Task OnSavedAsync(CatchEditSavedModel saved)
    {
        _catch = saved.Catch;
        Navigation.NavigateTo("/offline/catches");
        return Task.CompletedTask;
    }

    private void OnEditorBindingFailed(CatchEditSavedModel? saved)
    {
        _loadFailed = true;
    }
}
