using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.Catch.Pages.OfflineRecordCatch;

public partial class OfflineRecordCatch : ComponentBase
{
    private Guid _ownerUserId;
    private AnglerPreferencesModel _preferences = AnglerPreferencesModel.Empty;
    private bool _isLoading = true;

    [Inject] private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;
    [Inject] private IAnglerPreferencesStore AnglerPreferencesStore { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        var owner = OfflineOwnerContext.Owner;
        if (owner is null)
        {
            return;
        }

        _ownerUserId = owner.UserId;
        _preferences = await AnglerPreferencesStore.GetAsync(owner.UserId, CancellationToken.None)
            ?? AnglerPreferencesModel.Empty;
        _isLoading = false;
    }
}
