using FishingLogBook.Mobile.Features.Catch.Models;

namespace FishingLogBook.Mobile.Navigation;

public sealed class NavigationService(IRouteNavigator navigator)
{
    private readonly SemaphoreSlim _transition = new(1, 1);

    public async Task NavigateAsync(MobileRouteEnum route, CatchPresentationModel? item = null, CancellationToken cancellationToken = default)
    {
        var path = RouteConstants.GetPath(route);
        if (route is MobileRouteEnum.CatchDetail or MobileRouteEnum.PhotoPreview && item is null)
        {
            throw new ArgumentException("A catch is required for this destination.", nameof(item));
        }

        if (route == MobileRouteEnum.PhotoPreview && item?.HasPhoto != true)
        {
            return;
        }

        await TransitionAsync(path, item, cancellationToken);
    }

    public async Task GoBackAsync(CancellationToken cancellationToken = default)
    {
        if (navigator.CanGoBack)
        {
            await TransitionAsync("..", null, cancellationToken);
        }
    }

    private async Task TransitionAsync(string path, CatchPresentationModel? item, CancellationToken cancellationToken)
    {
        if (!await _transition.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            if (navigator.CurrentPath == path || navigator.CurrentPath.EndsWith('/' + path, StringComparison.Ordinal))
            {
                return;
            }

            await navigator.GoToAsync(path, item, cancellationToken);
        }
        finally
        {
            _transition.Release();
        }
    }
}
