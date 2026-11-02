using System.ComponentModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.ViewModels;
using FishingLogBook.Mobile.Features.Measurements.Popups;
using FishingLogBook.Mobile.Features.Measurements.ViewModels;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Popups;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Native.Controls;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Features.Import.Pages;

public partial class ImportPage : ContentPage
{
    private readonly ImportViewModel _viewModel;
    private readonly NavigationService _navigation;
    private bool _initialized;

    public ImportPage(ImportViewModel viewModel, NavigationService navigation)
    {
        _viewModel = viewModel;
        _navigation = navigation;
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        Unloaded += OnUnloaded;
        await _viewModel.LoadAsync(CancellationToken.None);
    }

    private async void OnUnloaded(object? sender, EventArgs args)
    {
        Unloaded -= OnUnloaded;
        if (!_viewModel.IsSuccessStep)
        {
            await _viewModel.ClearAsync();
        }
    }

    private void OnBack(object? sender, EventArgs args)
    {
        _viewModel.Back();
    }

    private async void OnContinue(object? sender, EventArgs args)
    {
        if (_viewModel.IsSuccessStep)
        {
            await _navigation.NavigateAsync(MobileRouteEnum.Catches);
            return;
        }

        await _viewModel.ContinueAsync(CancellationToken.None);
    }

    private async void OnViewTrips(object? sender, EventArgs args)
    {
        await _navigation.NavigateAsync(MobileRouteEnum.Trips);
    }

    private async void OnMethodChip(object? sender, EventArgs args)
    {
        if (sender is not ChipView { CommandParameter: ImportOptionModel option })
        {
            return;
        }

        if (_viewModel.ChooseMethod(option))
        {
            _viewModel.CompleteMethodPicker(await ShowCataloguePickerAsync(_viewModel.CreateMethodPicker()));
        }
    }

    private async void OnSpeciesChip(object? sender, EventArgs args)
    {
        if (sender is not ChipView { CommandParameter: ImportOptionModel option })
        {
            return;
        }

        if (_viewModel.ChooseSpecies(option))
        {
            _viewModel.CompleteSpeciesPicker(await ShowCataloguePickerAsync(_viewModel.CreateSpeciesPicker()));
        }
    }

    private async void OnPickPhotos(object? sender, EventArgs args)
    {
        await _viewModel.PickPhotosAsync(CancellationToken.None);
    }

    private async void OnRemovePhoto(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid photoId })
        {
            await _viewModel.RemovePhotoAsync(photoId, CancellationToken.None);
        }
    }

    private void OnKeepDuplicate(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid photoId })
        {
            _viewModel.KeepDuplicate(photoId);
        }
    }

    private void OnToggleCatchPhoto(object? sender, TappedEventArgs args)
    {
        if ((sender as BindableObject)?.BindingContext is ImportCatchPhotoModel photo)
        {
            _viewModel.TogglePhotoSelection(photo.CatchProposalId, photo.Photo.Id);
        }
    }

    private void OnSplitCatch(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid catchId })
        {
            _viewModel.SplitSelectedPhotos(catchId);
        }
    }

    private async void OnRemoveSelectedPhotos(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid catchId })
        {
            await _viewModel.RemoveSelectedPhotosAsync(catchId, CancellationToken.None);
        }
    }

    private async void OnCaughtOnDateChanged(object? sender, DateChangedEventArgs args)
    {
        if (sender is DatePicker { BindingContext: ImportCatchRowModel row })
        {
            await _viewModel.ConfirmCaughtOnAsync(row.Proposal.Id, row.CaughtOnLocal, CancellationToken.None);
        }
    }

    private async void OnCaughtOnTimeChanged(object? sender, EventArgs args)
    {
        if (sender is TimePicker picker
            && args is PropertyChangedEventArgs { PropertyName: nameof(TimePicker.Time) }
            && picker.BindingContext is ImportCatchRowModel row)
        {
            await _viewModel.ConfirmCaughtOnAsync(row.Proposal.Id, row.CaughtOnLocal, CancellationToken.None);
        }
    }

    private void OnUseShownValues(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid catchId })
        {
            _viewModel.UseShownValues(catchId);
        }
    }

    private async void OnCatchMethodChip(object? sender, EventArgs args)
    {
        if (sender is not ChipView { CommandParameter: ImportOptionModel option } || option.CatchId is not Guid catchId)
        {
            return;
        }

        if (_viewModel.ChooseCatchMethod(option))
        {
            _viewModel.CompleteCatchMethodPicker(
                catchId,
                await ShowCataloguePickerAsync(_viewModel.CreateCatchMethodPicker(catchId)));
        }
    }

    private async void OnCatchSpeciesChip(object? sender, EventArgs args)
    {
        if (sender is not ChipView { CommandParameter: ImportOptionModel option } || option.CatchId is not Guid catchId)
        {
            return;
        }

        if (_viewModel.ChooseCatchSpecies(option))
        {
            _viewModel.CompleteCatchSpeciesPicker(
                catchId,
                await ShowCataloguePickerAsync(_viewModel.CreateCatchSpeciesPicker(catchId)));
        }
    }

    private async void OnEditWeight(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid catchId })
        {
            await EditMeasurementAsync(
                _viewModel.CreateWeightEditor(catchId),
                (applied, editor) => _viewModel.CompleteWeightEdit(catchId, applied, editor));
        }
    }

    private async void OnEditLength(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid catchId })
        {
            await EditMeasurementAsync(
                _viewModel.CreateLengthEditor(catchId),
                (applied, editor) => _viewModel.CompleteLengthEdit(catchId, applied, editor));
        }
    }

    private async void OnEditPlaceName(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid catchId })
        {
            await _viewModel.EditPlaceNameAsync(catchId);
        }
    }

    private void OnRemoveLocation(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid catchId })
        {
            _viewModel.RemoveLocation(catchId);
        }
    }

    private void OnAcceptLocation(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: ImportLocationChoiceModel choice })
        {
            _viewModel.AcceptLocation(choice.CatchProposalId, choice.Location);
        }
    }

    private void OnMergeCatch(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: ImportMergeTargetModel target })
        {
            _viewModel.MergeCatches(target.SourceCatchId, target.CatchProposalId);
        }
    }

    private void OnConfirmCatch(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid catchId })
        {
            _viewModel.ConfirmCatch(catchId);
        }
    }

    private async void OnRemoveCatch(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid catchId })
        {
            await _viewModel.RemoveCatchAsync(catchId, CancellationToken.None);
        }
    }

    private void OnCreateTrip(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid tripId })
        {
            _viewModel.DecideCreateTrip(tripId);
        }
    }

    private void OnKeepSeparate(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid tripId })
        {
            _viewModel.DecideKeepSeparate(tripId);
        }
    }

    private async void OnUseExistingTrip(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: Guid tripId })
        {
            await _viewModel.DecideExistingTripAsync(tripId);
        }
    }

    private void OnRemoveCatchFromTrip(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: ImportTripCatchModel item })
        {
            _viewModel.RemoveCatchFromTrip(item.TripId, item.CatchId);
        }
    }

    private void OnCancelPersistence(object? sender, EventArgs args)
    {
        _viewModel.CancelPersistence();
    }

    private async Task EditMeasurementAsync(
        MeasurementEditorViewModel editor,
        Action<bool, MeasurementEditorViewModel> complete)
    {
        complete(await MeasurementEditorPopup.ShowAsync(this, editor), editor);
    }

    private async Task<CataloguePickerResultModel?> ShowCataloguePickerAsync(CataloguePickerViewModel picker)
    {
        var result = await this.ShowPopupAsync<CataloguePickerResultModel>(
            new CataloguePickerPopup(picker),
            new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true });
        return result.WasDismissedByTappingOutsideOfPopup ? null : result.Result;
    }
}
