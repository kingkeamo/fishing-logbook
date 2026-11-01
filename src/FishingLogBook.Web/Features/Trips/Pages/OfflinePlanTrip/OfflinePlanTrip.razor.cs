using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Pages.OfflinePlanTrip;

public partial class OfflinePlanTrip : ComponentBase
{
    private Guid _viewerUserId;
    private bool _accessLocked;

    [Parameter]
    public Guid? TripId { get; set; }

    [Inject]
    private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override void OnInitialized()
    {
        var owner = OfflineOwnerContext.Owner;
        _accessLocked = owner is null;
        _viewerUserId = owner?.UserId ?? Guid.Empty;
    }

    private void ReturnToTrips()
    {
        Navigation.NavigateTo("/offline/catches");
    }
}
