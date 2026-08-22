using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Api.Configuration;

public sealed class BuildMetadataConfig
{
    public const string SectionName = "Build";

    public string Version { get; set; } = string.Empty;

    public string Sha { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public void EnsureRequired()
    {
        if (string.IsNullOrWhiteSpace(Version)
            || string.IsNullOrWhiteSpace(Sha)
            || string.IsNullOrWhiteSpace(Environment))
        {
            throw new InvalidOperationException("Build metadata is incomplete.");
        }

        if (Environment.Equals("prod", StringComparison.OrdinalIgnoreCase)
            && (!System.Text.RegularExpressions.Regex.IsMatch(Version, @"^\d+\.\d+\.\d+$")
                || !System.Text.RegularExpressions.Regex.IsMatch(Sha, "^[0-9a-fA-F]{40}$")))
        {
            throw new InvalidOperationException("Production build metadata must contain a semantic version and full commit SHA.");
        }
    }

    public BuildMetadataDto ToDto()
    {
        return new BuildMetadataDto(Version, Sha, Environment, Timestamp);
    }
}
