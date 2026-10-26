namespace FishingLogBook.Mobile.Native.Controls;

public partial class ContentStateView : ContentView
{
    public ContentStateView()
    {
        InitializeComponent();
    }

    public event EventHandler? RetryRequested;

    private void OnRetry(object? sender, EventArgs args)
    {
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }
}
