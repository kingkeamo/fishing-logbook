using FishingLogBook.Mobile.IO.Adapters;

namespace FishingLogBook.Mobile.Native.Adapters;

public sealed class ClipboardAdapter : IClipboardAdapter
{
    public Task SetTextAsync(string text)
    {
        return Clipboard.Default.SetTextAsync(text);
    }
}
