using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.OfflineRecordCatch;

public partial class OfflineRecordCatch : ComponentBase
{
    [SupplyParameterFromQuery(Name = "tripId")]
    [Parameter]
    public Guid? TripId { get; set; }

    private Guid _ownerUserId;
    private AnglerPreferencesModel _preferences = AnglerPreferencesModel.Empty;
    private bool _isLoading = true;
    private bool _loadFailed;

    [Inject] private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;
    [Inject] private IAnglerPreferencesStore AnglerPreferencesStore { get; set; } = default!;
    [Inject] private ILoggingService Logging { get; set; } = default!;
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var owner = OfflineOwnerContext.Owner
                ?? throw new InvalidOperationException("Offline access is locked.");
            _ownerUserId = owner.UserId;
            _preferences = await AnglerPreferencesStore.GetAsync(owner.UserId, CancellationToken.None)
                ?? AnglerPreferencesModel.Empty;
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            await Logging.LogErrorAsync("loading cached preferences for offline catch recording", exception, CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
        }
    }
}
