using System.Globalization;

namespace FishingLogBook.Web.Tests;

internal sealed class TestCulture : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;
    private readonly CultureInfo? _previousDefaultCulture;
    private readonly CultureInfo? _previousDefaultUiCulture;

    private TestCulture(string cultureName)
    {
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        _previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        _previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static TestCulture Use(string cultureName)
    {
        return new TestCulture(cultureName);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _previousDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _previousDefaultUiCulture;
    }
}
