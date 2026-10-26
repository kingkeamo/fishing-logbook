using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Presentation;

namespace FishingLogBook.Mobile.Native.Pages;

public partial class PlannedPage : ContentPage
{
    private readonly UiTextService _text;

    public PlannedPage(UiTextService text, string titleKey)
    {
        _text = text;
        InitializeComponent();
        SetBinding(TitleProperty, new Binding("[" + titleKey + "]", source: text));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StateView.BindingContext = new ContentStateModel(ContentStateEnum.Planned, _text);
    }
}
