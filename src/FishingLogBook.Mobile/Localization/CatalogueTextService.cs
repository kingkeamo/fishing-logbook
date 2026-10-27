using System.Resources;

namespace FishingLogBook.Mobile.Localization;

public sealed class CatalogueTextService(UiTextService text)
{
    private static readonly ResourceManager FishingMethods = new("FishingLogBook.Mobile.Localization.FishingMethodStrings", typeof(CatalogueTextService).Assembly);
    private static readonly ResourceManager Species = new("FishingLogBook.Mobile.Localization.SpeciesStrings", typeof(CatalogueTextService).Assembly);

    public string GetFishingMethodLabel(string code, string canonicalName)
    {
        return Resolve(FishingMethods, code, canonicalName);
    }

    public string GetSpeciesLabel(string code, string canonicalName)
    {
        return Resolve(Species, code, canonicalName);
    }

    private string Resolve(ResourceManager resources, string code, string canonicalName)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return canonicalName;
        }

        var translation = resources.GetString(code, text.Culture);
        return string.IsNullOrWhiteSpace(translation) ? canonicalName : translation;
    }
}
