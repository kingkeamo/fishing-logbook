using System.Globalization;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Localization;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.Services.PreferenceServiceTests;

public abstract class BasePreferenceServiceTest
{
    protected readonly IPreferenceStore MockStore = Substitute.For<IPreferenceStore>();
    protected readonly IAppearanceService MockAppearance = Substitute.For<IAppearanceService>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected PreferenceService CreateSut()
    {
        return new PreferenceService(MockStore, MockAppearance, Text);
    }
}
