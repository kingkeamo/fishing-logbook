using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Trips.Modals.StartPlannedTrip;

public partial class StartPlannedTripModal : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly Dictionary<Guid, string> _whenLabels = [];
    private bool _isBusy;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public StartPlannedTripModalModel Model { get; set; } = default!;

    [Inject]
    private ITripDisplayService TripDisplay { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        foreach (var trip in Model.Trips)
        {
            var label = await TripDisplay.DescribeInstantAsync(trip.PlannedStartOn, _cancellationTokenSource.Token);
            _whenLabels[trip.TripId] = label ?? trip.PlannedStartOn.ToString("g");
        }
    }

    private string TripName(PlannedTripOptionModel trip)
    {
        return string.IsNullOrWhiteSpace(trip.Title) ? Loc["Trip_PlannedUntitled"].Value : trip.Title;
    }

    private string WhenLabel(PlannedTripOptionModel trip)
    {
        return _whenLabels.TryGetValue(trip.TripId, out var label)
            ? string.Format(Loc["Trip_PlannedFor"].Value, label)
            : string.Empty;
    }

    private string OrganiserLabel(PlannedTripOptionModel trip)
    {
        if (trip.IsOrganiser)
        {
            return Loc["Trip_PlannedOrganiserYou"].Value;
        }

        return string.Format(
            Loc["Trip_PlannedOrganiser"].Value,
            string.IsNullOrWhiteSpace(trip.OrganiserName) ? Loc["Trip_ContributorUnknown"].Value : trip.OrganiserName);
    }

    private string GuestsLabel(PlannedTripOptionModel trip)
    {
        var names = trip.Guests.Select(guest =>
        {
            var name = string.IsNullOrWhiteSpace(guest.DisplayName) ? Loc["Trip_ContributorUnknown"].Value : guest.DisplayName;
            return guest.IsPending ? string.Format(Loc["Trip_PlannedGuestInvited"].Value, name) : name;
        });
        return string.Format(Loc["Trip_PlannedGuests"].Value, string.Join(", ", names));
    }

    private void Select(PlannedTripOptionModel trip)
    {
        _isBusy = true;
        MudDialog.Close(DialogResult.Ok(new StartPlannedTripModalResult(trip)));
    }

    private void StartNew()
    {
        _isBusy = true;
        MudDialog.Close(DialogResult.Ok(new StartPlannedTripModalResult(Selected: null)));
    }

    private void Dismiss()
    {
        MudDialog.Cancel();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
