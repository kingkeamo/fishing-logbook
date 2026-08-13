namespace FishingLogBook.Web.Configuration;

public sealed class ApiConfig
{
    public const string SectionName = "Api";

    public string BaseUrl { get; set; } = string.Empty;
}
