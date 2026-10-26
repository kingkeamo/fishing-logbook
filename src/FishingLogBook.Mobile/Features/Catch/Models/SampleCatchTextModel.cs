namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed record SampleCatchTextModel(
    string? SpeciesKey,
    string? PlaceKey,
    string? NotesKey,
    string? MethodKey,
    CatchMethodEnum Method);
