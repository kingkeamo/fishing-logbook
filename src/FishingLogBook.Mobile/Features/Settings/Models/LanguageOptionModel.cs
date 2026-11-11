namespace FishingLogBook.Mobile.Features.Settings.Models;

public sealed record LanguageOptionModel(string CultureName, string Name, string FlagSource)
{
    public bool IsSelected { get; init; }
    public string AccessibilityDescription { get; init; } = string.Empty;
    public string AutomationId => $"Language{CultureName}";
}
