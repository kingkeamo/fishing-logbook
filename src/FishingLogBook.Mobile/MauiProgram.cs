using FishingLogBook.Mobile.Features.Catch.Pages;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddSingleton<UiTextService>();
        builder.Services.AddSingleton<CatchListViewModel>();
        builder.Services.AddSingleton<CatchSummaryPage>();
        builder.Services.AddTransient<CatchDetailPage>();
        builder.Services.AddTransient<PhotoPreviewPage>();
        builder.Services.AddSingleton<AppShell>();
        return builder.Build();
    }
}
