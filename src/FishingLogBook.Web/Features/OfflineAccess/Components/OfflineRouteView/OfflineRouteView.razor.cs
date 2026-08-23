using FishingLogBook.Web.Features.OfflineAccess.Services;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Features.OfflineAccess.Components.OfflineRouteView;

public partial class OfflineRouteView : ComponentBase
{
    [Inject] private IOfflineOwnerContextService OfflineOwnerContext { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Parameter, EditorRequired] public RouteData RouteData { get; set; } = default!;

    protected override void OnInitialized()
    {
        if (!OfflineOwnerContext.IsUnlocked)
        {
            Navigation.NavigateTo("/", replace: true);
        }
    }
}
