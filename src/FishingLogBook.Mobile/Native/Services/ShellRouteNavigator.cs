using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Native.Services;

public sealed class ShellRouteNavigator : IRouteNavigator
{
    public string CurrentPath => Shell.Current.CurrentState.Location.OriginalString;
    public bool CanGoBack => Shell.Current.Navigation.NavigationStack.Count > 1;

    public async Task GoToAsync(string path, CatchPresentationModel? item, Guid? catchEditId, Guid? tripId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var parameters = new ShellNavigationQueryParameters();
            if (item is not null)
            {
                parameters[RouteConstants.CatchParameter] = item;
            }

            if (catchEditId is { } id && id != Guid.Empty)
            {
                parameters[RouteConstants.CatchEditIdParameter] = id;
            }

            if (tripId is { } trip && trip != Guid.Empty)
            {
                parameters[RouteConstants.TripIdParameter] = trip;
            }

            Shell.Current.FlyoutIsPresented = false;
            await Shell.Current.GoToAsync(path, parameters);
        });
    }
}
