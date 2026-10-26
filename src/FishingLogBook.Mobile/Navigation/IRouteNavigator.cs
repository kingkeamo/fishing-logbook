using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Navigation;

public interface IRouteNavigator
{
    string CurrentPath { get; }
    bool CanGoBack { get; }
    Task GoToAsync(string path, CatchPresentationModel? item, CancellationToken cancellationToken);
}
