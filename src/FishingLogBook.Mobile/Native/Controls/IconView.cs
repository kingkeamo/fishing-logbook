using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Extensions;

namespace FishingLogBook.Mobile.Native.Controls;

public class IconView : Image
{
    public static readonly BindableProperty ToneProperty = BindableProperty.Create(
        nameof(Tone),
        typeof(IconToneEnum),
        typeof(IconView),
        IconToneEnum.Default,
        propertyChanged: (bindable, _, tone) => ((IconView)bindable).ApplyTone((IconToneEnum)tone));

    private readonly IconTintColorBehavior _tint = new();

    public IconView()
    {
        Aspect = Aspect.AspectFit;
        Behaviors.Add(_tint);
        AutomationProperties.SetIsInAccessibleTree(this, false);
        ApplyTone(IconToneEnum.Default);
    }

    public IconToneEnum Tone
    {
        get => (IconToneEnum)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    private static (string Light, string Dark) Keys(IconToneEnum tone)
    {
        return tone switch
        {
            IconToneEnum.Muted => ("MutedLight", "MutedDark"),
            IconToneEnum.Accent => ("Primary", "PrimaryDark"),
            IconToneEnum.Link => ("Teal", "TealDark"),
            IconToneEnum.Danger => ("DangerLight", "DangerDark"),
            IconToneEnum.Inverse => ("Paper", "Paper"),
            _ => ("Navy", "Sky")
        };
    }

    private void ApplyTone(IconToneEnum tone)
    {
        var resources = Application.Current?.Resources;
        var (light, dark) = Keys(tone);
        if (resources is null || !resources.TryGetValue(light, out var lightColor) || !resources.TryGetValue(dark, out var darkColor))
        {
            return;
        }

        _tint.SetAppThemeColor(IconTintColorBehavior.TintColorProperty, (Color)lightColor, (Color)darkColor);
    }
}
