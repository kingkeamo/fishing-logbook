using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Pages.OfflineCatchList;

public partial class OfflineCatchList : ComponentBase
{
    private IReadOnlyList<CatchModel> _catches = [];
    private Guid _ownerUserId;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _isLoading = true;
    private bool _loadFailed;

    [Inject] private ICatchStore CatchStore { get; set; } = default!;
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
            _catches = LocalCatchVisibility.ForOwner(
                await CatchStore.GetAllAsync(owner.UserId, CancellationToken.None),
                owner.UserId);
            var preferences = await AnglerPreferencesStore.GetAsync(owner.UserId, CancellationToken.None);
            if (preferences is not null)
            {
                _weightUnit = preferences.WeightUnit;
                _lengthUnit = preferences.LengthUnit;
            }
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading offline catches", exception, CancellationToken.None);
            _loadFailed = true;
        }
        finally
        {
            _isLoading = false;
        }
    }
}
