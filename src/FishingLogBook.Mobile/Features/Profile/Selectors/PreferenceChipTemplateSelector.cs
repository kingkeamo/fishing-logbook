using FishingLogBook.Mobile.Features.Profile.Models;

namespace FishingLogBook.Mobile.Features.Profile.Selectors;

public sealed class PreferenceChipTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplate? MoreTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        var template = item is PreferenceMoreActionModel ? MoreTemplate : ItemTemplate;
        return template ?? throw new InvalidOperationException("Both preference chip templates must be set.");
    }
}
