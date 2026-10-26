namespace FishingLogBook.Mobile.Native.Controls;

public partial class ContentStateView : ContentView
{
    public ContentStateView()
    {
        InitializeComponent();
    }

    public event EventHandler? RetryRequested;
    public event EventHandler? ActionRequested;

    private void OnAction(object? sender, EventArgs args)
    {
        ActionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRetry(object? sender, EventArgs args)
    {
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }
}
