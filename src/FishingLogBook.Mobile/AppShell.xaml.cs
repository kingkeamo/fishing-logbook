using FishingLogBook.Mobile.Features.Catch.Pages;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile;

public partial class AppShell : Shell
{
    public AppShell(CatchSummaryPage summary, UiTextService text)
    {
        InitializeComponent();
        var content = new ShellContent { Route = "catches", Content = summary };
        content.SetBinding(TitleProperty, new Binding("[Catches]", source: text));
        Items.Add(content);
        Routing.RegisterRoute(nameof(CatchDetailPage), typeof(CatchDetailPage));
        Routing.RegisterRoute(nameof(PhotoPreviewPage), typeof(PhotoPreviewPage));
    }
}
