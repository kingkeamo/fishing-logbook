using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Tests.DependencyInjectionTests;

internal sealed class TestNavigationManager : NavigationManager
{
    public TestNavigationManager()
    {
        Initialize("https://localhost:5019/", "https://localhost:5019/");
    }

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
    }
}
