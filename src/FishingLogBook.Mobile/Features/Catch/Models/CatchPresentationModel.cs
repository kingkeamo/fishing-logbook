using System.ComponentModel;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed class CatchPresentationModel(
    CatchDto catchDto,
    UiTextService text,
    IReadOnlyList<string>? photos = null,
    SampleCatchTextModel? sampleText = null) : INotifyPropertyChanged
{
    private static readonly IMeasurementService DefaultMeasurement = new MeasurementService();
    private IMeasurementService _measurement = DefaultMeasurement;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;

    public UiTextService Text { get; } = text;
    public Guid Id => catchDto.Id;
    public string Species => LocalizedOrRecorded(sampleText?.SpeciesKey, catchDto.SpeciesName);
    public string CaughtOn => catchDto.CaughtOn.ToUniversalTime().ToString("f", Text.Culture);
    public string ShortDate => catchDto.CaughtOn.ToUniversalTime().ToString("dd MMM yyyy", Text.Culture);
    public DateTimeOffset RecordedAt => catchDto.CaughtOn.ToUniversalTime();
    public DateOnly Day => DateOnly.FromDateTime(RecordedAt.UtcDateTime);
    public string Time => RecordedAt.ToString("t", Text.Culture) + " UTC";
    public CatchMethodEnum MethodIdentifier => sampleText?.Method ?? ParseMethod(catchDto.Method);
    public string Method => LocalizedOrRecorded(sampleText?.MethodKey, catchDto.Method);
    public bool HasMethod => sampleText?.MethodKey is not null || !string.IsNullOrWhiteSpace(catchDto.Method);
    public bool HasMeasurements => catchDto.Weight.HasValue || catchDto.Length.HasValue;
    public string Measurements => string.Join(" · ", new[] { catchDto.Weight.HasValue ? Weight : null, catchDto.Length.HasValue ? Length : null }.Where(value => value is not null));
    public string CardDescription => string.Join(", ", new[] { OpenDescription, HasMethod ? Method : null, HasMeasurements ? Measurements : null }.Where(value => value is not null));
    public string Weight => FormatWeight();
    public string Length => FormatLength();
    public string Place => LocalizedOrRecorded(sampleText?.PlaceKey, catchDto.PlaceName);
    public string Notes => LocalizedOrRecorded(sampleText?.NotesKey, catchDto.Notes);
    public IReadOnlyList<string> Photos { get; } = photos ?? [];
    public string? PreviewAsset => Photos.Count > 0 ? Photos[0] : null;
    public int PhotoCount => Photos.Count;
    public bool HasPhoto => Photos.Count > 0;
    public bool HasMultiplePhotos => Photos.Count > 1;
    public string PhotoCounter => $"1/{Photos.Count}";
    public bool HasNotes => sampleText?.NotesKey is not null || !string.IsNullOrWhiteSpace(catchDto.Notes);
    public CatchSpeciesEnum SpeciesIdentifier => sampleText?.Species ?? CatchSpeciesEnum.Other;
    public bool HasNoPhoto => !HasPhoto;
    public string OpenDescription => string.Format(Text.Culture, Text["OpenCatchDescription"], Species, CaughtOn);
    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetUnits(IMeasurementService measurement, WeightUnitEnum weightUnit, LengthUnitEnum lengthUnit)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        _measurement = measurement;
        _weightUnit = weightUnit;
        _lengthUnit = lengthUnit;
        RefreshLocalizedText();
    }

    public void RefreshLocalizedText()
    {
        foreach (var property in new[] { nameof(Species), nameof(CaughtOn), nameof(ShortDate), nameof(Time), nameof(Method), nameof(Measurements), nameof(CardDescription), nameof(Weight), nameof(Length), nameof(Place), nameof(Notes), nameof(OpenDescription) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    private string LocalizedOrRecorded(string? resourceKey, string? recordedValue)
    {
        return resourceKey is null ? DisplayOrMissing(recordedValue) : Text[resourceKey];
    }

    private static CatchMethodEnum ParseMethod(string? method)
    {
        return Enum.TryParse(method, false, out CatchMethodEnum parsed) && parsed != CatchMethodEnum.All
            ? parsed
            : CatchMethodEnum.All;
    }

    private string DisplayOrMissing(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Text["NotRecorded"] : value;
    }

    private string FormatWeight()
    {
        var display = _measurement.ToDisplayWeight(catchDto.Weight, _weightUnit);
        if (display is null)
        {
            return Text["NotRecorded"];
        }

        var unit = Text[_weightUnit == WeightUnitEnum.Lb ? "MeasureUnitLb" : "MeasureUnitKg"];
        return $"{display.Value.ToString("0.##", Text.Culture)} {unit}";
    }

    private string FormatLength()
    {
        var display = _measurement.ToDisplayLength(catchDto.Length, _lengthUnit);
        if (display is null)
        {
            return Text["NotRecorded"];
        }

        var unit = Text[_lengthUnit == LengthUnitEnum.In ? "MeasureUnitIn" : "MeasureUnitCm"];
        return $"{display.Value.ToString("0.##", Text.Culture)} {unit}";
    }
}

