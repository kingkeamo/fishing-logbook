namespace FishingLogBook.Web.Features.Catch.Services;

public interface ICatchSessionService
{
    string? Method { get; }

    string? SpeciesName { get; }

    void Remember(string? method, string? speciesName);

    void Clear();
}
