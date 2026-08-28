using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripCatches;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.TripCatches;

public partial class TripCatches : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private bool _isBusy;
    private bool _someWereRejected;

    [Parameter]
    [EditorRequired]
    public TripModel Trip { get; set; } = default!;

    [Parameter]
    public string RecordCatchBaseHref { get; set; } = "/catches/record";

    [Parameter]
    public EventCallback OnCatchesAttached { get; set; }

    [Parameter]
    public bool ShowRecordAction { get; set; } = true;

    [Parameter]
    public bool ShowAddAction { get; set; } = true;

    [Parameter]
    public TripStorageEnum CatchStorage { get; set; } = TripStorageEnum.LocalFirst;

    [Parameter]
    public WeightUnitEnum WeightUnit { get; set; } = WeightUnitEnum.Kg;

    [Parameter]
    public LengthUnitEnum LengthUnit { get; set; } = LengthUnitEnum.Cm;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string RecordCatchHref => $"{RecordCatchBaseHref}?tripId={Trip.Id:D}";

    public async Task AddCatchesAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        _someWereRejected = false;
        try
        {
            var added = await ModalService
                .ShowAsync<AddTripCatchesModal, AddTripCatchesModalModel, AddTripCatchesModalResult>(
                    new AddTripCatchesModalModel(
                        new TripCatchScopeModel(
                            Trip.Id,
                            Trip.OwnerUserId,
                            Trip.StartedOn,
                            Trip.EndedOn),
                        CatchStorage,
                        WeightUnit,
                        LengthUnit),
                    _cancellationTokenSource.Token);
            if (added is null || added.AssociatedCatchIds.Count == 0)
            {
                return;
            }

            _someWereRejected = added.RejectedCatchIds.Count > 0;
            await OnCatchesAttached.InvokeAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
