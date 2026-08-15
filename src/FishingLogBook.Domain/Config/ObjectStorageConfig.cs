namespace FishingLogBook.Domain.Config;

public sealed class ObjectStorageConfig
{
    public const string SectionName = "ObjectStorage";

    public string ServiceUrl { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public bool IsConfigured =>
        IsProvided(ServiceUrl) &&
        IsProvided(BucketName) &&
        IsProvided(AccessKeyId) &&
        IsProvided(SecretAccessKey);

    private static bool IsProvided(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !value.Contains("user secrets", StringComparison.OrdinalIgnoreCase);
    }
}
