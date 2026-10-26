using FishingLogBook.Mobile.Navigation;

namespace FishingLogBook.Mobile.Tests.Navigation.RouteConstantsTests;

public abstract class BaseRouteConstantsTest
{
    protected IReadOnlyList<MobileRouteEnum> Primary => RouteConstants.Primary;
}
