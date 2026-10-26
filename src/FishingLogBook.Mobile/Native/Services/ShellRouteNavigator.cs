using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Native.Services;

public sealed class ShellRouteNavigator : IRouteNavigator
{
    public string CurrentPath => Shell.Current.CurrentState.Location.OriginalString;
    public bool CanGoBack => Shell.Current.Navigation.NavigationStack.Count > 1;

    public async Task GoToAsync(string path, CatchPresentationModel? item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var parameters = new ShellNavigationQueryParameters();
            if (item is not null)
            {
                parameters[RouteConstants.CatchParameter] = item;
            }

            Shell.Current.FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(path, parameters);
        });
    }
}
