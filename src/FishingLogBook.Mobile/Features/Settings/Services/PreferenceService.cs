using System.ComponentModel;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Mobile.Features.Settings.Services;

public sealed class PreferenceService : INotifyPropertyChanged
{
    public const string LanguageKey = "mobile.language";
    public const string AppearanceKey = "mobile.appearance";
    public const string WeightUnitKey = "mobile.weightUnit";
    public const string LengthUnitKey = "mobile.lengthUnit";
    private readonly IPreferenceStore _store;
    private readonly IAppearanceService _appearance;
    private bool _isInitialized;

    public PreferenceService(IPreferenceStore store, IAppearanceService appearance, UiTextService text)
    {
        _store = store;
        _appearance = appearance;
        Text = text;
    }

    public UiTextService Text { get; }
    public AppearanceEnum Appearance { get; private set; }
    public WeightUnitEnum WeightUnit { get; private set; } = WeightUnitEnum.Kg;
    public LengthUnitEnum LengthUnit { get; private set; } = LengthUnitEnum.Cm;
    public int LanguageIndex => Text.Culture.TwoLetterISOLanguageName == "fr" ? 1 : 0;
    public IReadOnlyList<string> Languages => [Text["LanguageEnglish"], Text["LanguageFrench"]];
    public IReadOnlyList<string> Themes => [Text["ThemeSystem"], Text["ThemeLight"], Text["ThemeDark"]];
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        var language = _store.Get(LanguageKey, Text.Culture.Name);
        if (language is "en-GB" or "fr-FR")
        {
            Text.SetCulture(language);
        }

        Appearance = _store.Get(AppearanceKey, "System") switch
        {
            "Light" => AppearanceEnum.Light,
            "Dark" => AppearanceEnum.Dark,
            _ => AppearanceEnum.System
        };
        _appearance.Apply(Appearance);
        WeightUnit = _store.Get(WeightUnitKey, nameof(WeightUnitEnum.Kg)) == nameof(WeightUnitEnum.Lb) ? WeightUnitEnum.Lb : WeightUnitEnum.Kg;
        LengthUnit = _store.Get(LengthUnitKey, nameof(LengthUnitEnum.Cm)) == nameof(LengthUnitEnum.In) ? LengthUnitEnum.In : LengthUnitEnum.Cm;
        _isInitialized = true;
    }

    public void SetWeightUnit(WeightUnitEnum unit)
    {
        if (!Enum.IsDefined(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit));
        }

        if (WeightUnit == unit)
        {
            return;
        }

        _store.Set(WeightUnitKey, unit.ToString());
        WeightUnit = unit;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WeightUnit)));
    }

    public void SetLengthUnit(LengthUnitEnum unit)
    {
        if (!Enum.IsDefined(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit));
        }

        if (LengthUnit == unit)
        {
            return;
        }

        _store.Set(LengthUnitKey, unit.ToString());
        LengthUnit = unit;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LengthUnit)));
    }

    public void SetLanguage(int index)
    {
        if (index is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (LanguageIndex == index)
        {
            return;
        }

        var culture = index == 1 ? "fr-FR" : "en-GB";
        _store.Set(LanguageKey, culture);
        Text.SetCulture(culture);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageIndex)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Themes)));
    }

    public void SetAppearance(AppearanceEnum appearance)
    {
        if (!Enum.IsDefined(appearance))
        {
            throw new ArgumentOutOfRangeException(nameof(appearance));
        }

        if (Appearance == appearance)
        {
            return;
        }

        _store.Set(AppearanceKey, appearance.ToString());
        _appearance.Apply(appearance);
        Appearance = appearance;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Appearance)));
    }
}
