using System.Globalization;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Tests.Localization.CatalogueTextServiceTests;

public abstract class BaseCatalogueTextServiceTest
{
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly CatalogueTextService Sut;

    protected BaseCatalogueTextServiceTest()
    {
        Sut = new CatalogueTextService(Text);
    }
}
