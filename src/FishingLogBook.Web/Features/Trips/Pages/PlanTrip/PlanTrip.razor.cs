using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Pages.PlanTrip;

public partial class PlanTrip : ComponentBase
{
    private Guid _viewerUserId;
    private bool _isLoading = true;

    [Parameter]
    public Guid? TripId { get; set; }

    [Inject]
    private ILocalCatchOwnerService LocalOwner { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _viewerUserId = await LocalOwner.GetUserIdAsync(CancellationToken.None);
        _isLoading = false;
    }

    private void ReturnToTrips()
    {
        Navigation.NavigateTo("/trips");
    }
}
