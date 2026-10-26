namespace FishingLogBook.Mobile.Features.Settings.Services;

public interface IPreferenceStore
{
    string Get(string key, string fallback);
    void Set(string key, string value);
}
