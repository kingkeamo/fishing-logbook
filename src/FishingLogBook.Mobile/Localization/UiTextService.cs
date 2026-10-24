using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace FishingLogBook.Mobile.Localization;

public sealed class UiTextService : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources = new("FishingLogBook.Mobile.Localization.UiStrings", typeof(UiTextService).Assembly);

    public UiTextService() : this(CultureInfo.CurrentUICulture)
    {
    }

    public UiTextService(CultureInfo culture)
    {
        Culture = ResolveCulture(culture);
    }

    public CultureInfo Culture { get; private set; }

    public string this[string key] => Resources.GetString(key, Culture) ?? throw new MissingManifestResourceException(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetCulture(string cultureName)
    {
        Culture = ResolveCulture(CultureInfo.GetCultureInfo(cultureName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
    }

    private static CultureInfo ResolveCulture(CultureInfo culture)
    {
        return CultureInfo.GetCultureInfo(culture.TwoLetterISOLanguageName == "fr" ? "fr-FR" : "en-GB");
    }
}
