using System.Globalization;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Tests.Localization.UiTextServiceTests;

public abstract class BaseUiTextServiceTest
{
    protected UiTextService Sut { get; } = new(CultureInfo.GetCultureInfo("en-GB"));
}
