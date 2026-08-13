namespace FishingLogBook.Web.Localization;

public interface ICultureService
{
    string CurrentCulture { get; }

    Task InitializeAsync();

    Task SetCultureAsync(string cultureName);
}
