namespace FishingLogBook.Mobile.IO.Adapters;

public interface IClipboardAdapter
{
    Task SetTextAsync(string text);
}
