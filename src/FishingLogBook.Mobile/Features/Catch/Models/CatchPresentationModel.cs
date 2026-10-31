using System.ComponentModel;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed class CatchPresentationModel(
    CatchDto catchDto,
    UiTextService text,
    IReadOnlyList<string>? photos = null,
    SampleCatchTextModel? sampleText = null,
    bool savedLocally = false,
    CatchSyncStatusModel? syncStatus = null) : INotifyPropertyChanged
{
    private static readonly IMeasurementService DefaultMeasurement = new MeasurementService();
    private IReadOnlyList<CatchPhotoThumbnailModel>? _thumbnails;
    private CatchSyncStatusModel? _syncStatus = syncStatus;
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
    public string CardDescription => string.Join(", ", new[] { OpenDescription, HasMethod ? Method : null, HasMeasurements ? Measurements : null, HasSaveStatus ? SaveStatus : null }.Where(value => value is not null));
    public bool HasSaveStatus => savedLocally;
    public string SaveStatus => savedLocally ? DescribeSaveStatus() : string.Empty;
    public CatchSyncStateEnum SyncState => _syncStatus?.State ?? CatchSyncStateEnum.WaitingToSync;
    public bool CanRetrySync => _syncStatus?.CanRetry == true;
    public string Weight => FormatWeight();
    public string Length => FormatLength();
    public string Place => LocalizedOrRecorded(sampleText?.PlaceKey, catchDto.PlaceName);
    public string Notes => LocalizedOrRecorded(sampleText?.NotesKey, catchDto.Notes);
    public string BaitOrLure => string.IsNullOrWhiteSpace(catchDto.BaitOrLure) ? Text["NoBait"] : catchDto.BaitOrLure;
    public IReadOnlyList<string> Photos { get; } = photos ?? [];
    public IReadOnlyList<CatchPhotoThumbnailModel> Thumbnails => _thumbnails ??= Photos.Select((path, index) => new CatchPhotoThumbnailModel(index, path, index == SelectedPhotoIndex, DescribePhoto)).ToArray();
    public int SelectedPhotoIndex { get; private set; }
    public string? SelectedPhoto => HasPhoto ? Photos[SelectedPhotoIndex] : null;
    public string? PreviewAsset => Photos.Count > 0 ? Photos[0] : null;
    public int PhotoCount => Photos.Count;
    public bool HasPhoto => Photos.Count > 0;
    public bool HasMultiplePhotos => Photos.Count > 1;
    public string PhotoPosition => DescribePhoto(HasPhoto ? SelectedPhotoIndex : -1);
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

    public void ApplySyncStatus(CatchSyncStatusModel? status)
    {
        _syncStatus = status;
        foreach (var property in new[] { nameof(SaveStatus), nameof(CardDescription), nameof(SyncState), nameof(CanRetrySync) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    public void SelectPhoto(int index)
    {
        if (index < 0 || index >= Photos.Count || index == SelectedPhotoIndex)
        {
            return;
        }

        Thumbnails[SelectedPhotoIndex].IsSelected = false;
        SelectedPhotoIndex = index;
        Thumbnails[index].IsSelected = true;
        foreach (var property in new[] { nameof(SelectedPhotoIndex), nameof(SelectedPhoto), nameof(PhotoPosition) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    public void NextPhoto()
    {
        if (HasMultiplePhotos)
        {
            SelectPhoto((SelectedPhotoIndex + 1) % Photos.Count);
        }
    }

    public void PreviousPhoto()
    {
        if (HasMultiplePhotos)
        {
            SelectPhoto((SelectedPhotoIndex - 1 + Photos.Count) % Photos.Count);
        }
    }

    public void ResetPhotoSelection()
    {
        SelectPhoto(0);
    }

    public void RefreshLocalizedText()
    {
        foreach (var property in new[] { nameof(Species), nameof(CaughtOn), nameof(ShortDate), nameof(Time), nameof(Method), nameof(Measurements), nameof(CardDescription), nameof(Weight), nameof(Length), nameof(Place), nameof(Notes), nameof(BaitOrLure), nameof(SaveStatus), nameof(OpenDescription), nameof(PhotoPosition) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        foreach (var thumbnail in Thumbnails)
        {
            thumbnail.RefreshDescription();
        }
    }

    private string DescribeSaveStatus()
    {
        if (_syncStatus is not { } status)
        {
            return Text["CatchSavedLocally"];
        }

        var parts = new List<string>();
        if (status.MetadataSynced && status.PhotographsRecorded < status.PhotographCount)
        {
            parts.Add(string.Format(Text.Culture, Text["CatchSyncPhotosProgress"], status.PhotographsRecorded, status.PhotographCount));
        }
        else if (status.State == CatchSyncStateEnum.WaitingToSync)
        {
            parts.Add(Text["CatchSavedLocally"]);
        }

        parts.Add(Text[status.State switch
        {
            CatchSyncStateEnum.Synced => "CatchSyncSynced",
            CatchSyncStateEnum.Synchronising => "CatchSyncSynchronising",
            CatchSyncStateEnum.NeedsAttention => "CatchSyncNeedsAttention",
            CatchSyncStateEnum.ActionRequired => "CatchSyncActionRequired",
            _ => "CatchSyncWaiting"
        }]);
        return string.Join(" ", parts);
    }

    private string DescribePhoto(int index)
    {
        return string.Format(Text.Culture, Text["PhotoPosition"], index + 1, Photos.Count);
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

