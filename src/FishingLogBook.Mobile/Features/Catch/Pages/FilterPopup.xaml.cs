using CommunityToolkit.Maui.Views;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Native.Controls;

namespace FishingLogBook.Mobile.Features.Catch.Pages;

public partial class FilterPopup : Popup<CatchFilterModel>
{
    private readonly CatchFilterDraftModel _draft;
    private bool _closing;

    public FilterPopup(CatchFilterDraftModel draft)
    {
        _draft = draft;
        InitializeComponent();
        BindingContext = draft;
        Sheet.MaximumHeightRequest = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density * 0.9;
    }

    private void OnMethod(object? sender, EventArgs args)
    {
        if (sender is ChipView { CommandParameter: CatchMethodEnum method })
        {
            _draft.SetMethod(method);
        }
    }

    private void OnDateRange(object? sender, EventArgs args)
    {
        if (sender is ChipView { CommandParameter: CatchDateRangeEnum range })
        {
            _draft.SetDateRange(range);
        }
    }

    private void OnLocationChanged(object? sender, TextChangedEventArgs args)
    {
        if (_draft.Location != args.NewTextValue)
        {
            _draft.SetLocation(args.NewTextValue);
        }
    }

    private void OnClearAll(object? sender, EventArgs args)
    {
        _draft.Clear();
    }

    private async void OnApply(object? sender, EventArgs args)
    {
        await CompleteAsync(_draft.ToFilter());
    }

    private async void OnClose(object? sender, EventArgs args)
    {
        await CompleteAsync(null);
    }

    private async Task CompleteAsync(CatchFilterModel? filter)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        if (filter is null)
        {
            await CloseAsync();
            return;
        }

        await CloseAsync(filter);
    }
}
