using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed class CatchPresentationModel(CatchDto catchDto, UiTextService text, string? previewAsset = null, bool isSample = false)
{
    public UiTextService Text { get; } = text;
    public Guid Id => catchDto.Id;
    public string Species => isSample ? Text[catchDto.SpeciesName == "Atlantic salmon" ? "SampleSalmon" : "SampleSpecies"] : DisplayOrMissing(catchDto.SpeciesName);
    public string CaughtOn => catchDto.CaughtOn.ToUniversalTime().ToString("f", Text.Culture);
    public string ShortDate => catchDto.CaughtOn.ToUniversalTime().ToString("dd MMM yyyy", Text.Culture);
    public DateTimeOffset RecordedAt => catchDto.CaughtOn.ToUniversalTime();
    public DateOnly Day => DateOnly.FromDateTime(RecordedAt.UtcDateTime);
    public string Time => RecordedAt.ToString("t", Text.Culture) + " UTC";
    public string MethodCode => catchDto.Method ?? string.Empty;
    public string Method => isSample && MethodCode is "Spinning" or "Fly" ? Text["Method" + MethodCode] : DisplayOrMissing(catchDto.Method);
    public bool HasMethod => !string.IsNullOrWhiteSpace(catchDto.Method);
    public bool HasMeasurements => catchDto.Weight.HasValue || catchDto.Length.HasValue;
    public string Measurements => string.Join(" · ", new[] { catchDto.Weight.HasValue ? Weight : null, catchDto.Length.HasValue ? Length : null }.Where(value => value is not null));
    public string CardDescription => string.Join(", ", new[] { OpenDescription, HasMethod ? Method : null, HasMeasurements ? Measurements : null }.Where(value => value is not null));
    public string Weight => FormatMeasurement(catchDto.Weight, "kg");
    public string Length => FormatMeasurement(catchDto.Length, "cm");
    public string Place => isSample ? Text["SamplePlace"] : DisplayOrMissing(catchDto.PlaceName);
    public string Notes => isSample ? Text["SampleNotes"] : DisplayOrMissing(catchDto.Notes);
    public string? PreviewAsset { get; } = previewAsset;
    public bool HasPhoto => PreviewAsset is not null;
    public bool HasNoPhoto => !HasPhoto;
    public string OpenDescription => string.Format(Text.Culture, Text["OpenCatchDescription"], Species, CaughtOn);

    private string DisplayOrMissing(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Text["NotRecorded"] : value;
    }

    private string FormatMeasurement(decimal? value, string unit)
    {
        return value.HasValue ? $"{value.Value.ToString("0.##", Text.Culture)} {unit}" : Text["NotRecorded"];
    }
}
