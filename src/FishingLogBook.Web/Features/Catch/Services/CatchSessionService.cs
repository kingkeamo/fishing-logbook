namespace FishingLogBook.Web.Features.Catch.Services;

public sealed class CatchSessionService : ICatchSessionService
{
    public string? Method { get; private set; }

    public string? SpeciesName { get; private set; }

    public void Remember(string? method, string? speciesName)
    {
        Method = Normalise(method);
        SpeciesName = Normalise(speciesName);
    }

    public void Clear()
    {
        Method = null;
        SpeciesName = null;
    }

    private static string? Normalise(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
