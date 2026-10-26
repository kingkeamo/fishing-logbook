using Android.Content.Res;
using Microsoft.Maui.Handlers;

namespace FishingLogBook.Mobile;

public static partial class MauiProgram
{
    static partial void ConfigurePlatformHandlers()
    {
        EntryHandler.Mapper.AppendToMapping("BorderlessEntry", static (handler, _) =>
        {
            handler.PlatformView.BackgroundTintList = ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        });
    }
}
