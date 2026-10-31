using System.ComponentModel;
using System.Globalization;
using FishingLogBook.Mobile.Features.Measurements.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;

namespace FishingLogBook.Mobile.Features.Measurements.ViewModels;

public sealed class MeasurementEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private const int OuncesPerPound = 16;
    private readonly IMeasurementService _measurement;
    private readonly UiTextService _text;
    private decimal? _canonicalValue;
    private MeasurementScaleRangeModel _range;
    private WeightUnitEnum _weightUnit;
    private LengthUnitEnum _lengthUnit;
    private int _pounds;
    private int _ounces;
    private bool _hasInvalidInput;
    private bool _suppressEntryCommit;
    private bool _disposed;

    public MeasurementEditorViewModel(
        IMeasurementService measurement,
        UiTextService text,
        MeasurementKindEnum kind,
        decimal? canonicalValue,
        WeightUnitEnum weightUnit,
        LengthUnitEnum lengthUnit)
    {
        _measurement = measurement;
        _text = text;
        Kind = kind;
        _weightUnit = weightUnit;
        _lengthUnit = lengthUnit;
        _canonicalValue = canonicalValue;
        _range = MeasurementScaleRangeModel.SmallestFor(IsWeight, canonicalValue);
        SynchroniseInputs();
        _text.PropertyChanged += OnCultureChanged;
    }

    public UiTextService Text => _text;

    public MeasurementKindEnum Kind { get; }
    public bool IsWeight => Kind == MeasurementKindEnum.Weight;
    public bool IsImperialWeight => IsWeight && _weightUnit == WeightUnitEnum.Lb;
    public bool IsSingleValue => !IsImperialWeight;
    public decimal? CanonicalValue => _canonicalValue;
    public WeightUnitEnum WeightUnit => _weightUnit;
    public LengthUnitEnum LengthUnit => _lengthUnit;
    public IReadOnlyList<MeasurementScaleRangeModel> Ranges => MeasurementScaleRangeModel.For(IsWeight);
    public MeasurementScaleRangeModel SelectedRange => _range;
    public int Pounds => _pounds;
    public int Ounces => _ounces;
    public bool IsValid => !_hasInvalidInput && ValidationMessage is null;
    public bool HasValidationMessage => ValidationMessage is not null;
    public string Title => _text[IsWeight ? "MeasureWeight" : "MeasureLength"];
    public string ExactLabel => string.Format(_text.Culture, _text["MeasureExact"], UnitLabel);
    public string SliderDescription => string.Format(_text.Culture, _text["MeasureSliderLabel"], Title);
    public string SliderHint => _text["MeasureSlide"];
    public string DisplayText => _canonicalValue is null ? _text["MeasureNotRecorded"] : FormatCanonical(_canonicalValue.Value);
    public string CurrentDescription => string.Format(_text.Culture, _text["MeasureCurrent"], Title, DisplayText);
    public string SliderMinimumLabel => FormatCanonical(0m);
    public string SliderMaximumLabel => FormatCanonical(_range.MaximumCanonical);
    public decimal SliderMaximum => DisplayOf(_range.MaximumCanonical);
    public decimal SliderValue => Math.Clamp(DisplayValue ?? 0m, 0m, SliderMaximum);
    public decimal Fraction => SliderMaximum <= 0m ? 0m : SliderValue / SliderMaximum;
    public double FractionValue => (double)Fraction;
    public string RangeGroupLabel => _text["MeasureRangeGroup"];
    public string PoundsLabel => _text["MeasurePounds"];
    public string OuncesLabel => _text["MeasureOunces"];

    public string RangeLabel(MeasurementScaleRangeModel range)
    {
        return _text["MeasureRange" + range.Range];
    }

    public string RangeHint => _text["MeasureRangeHint"];

    public string RangeDescription(MeasurementScaleRangeModel range)
    {
        var label = RangeLabel(range);
        if (!range.CanDisplay(_canonicalValue))
        {
            return string.Format(_text.Culture, _text["MeasureRangeUnavailable"], label);
        }

        return range == _range ? string.Format(_text.Culture, _text["MeasureRangeSelected"], label) : label;
    }

    public decimal SliderStep => SliderStepFor(Kind, _weightUnit, _lengthUnit);
    public decimal FineStep => FineStepFor(Kind, _weightUnit, _lengthUnit);
    public string FineStepText => IsImperialWeight ? _text["MeasureOneOunce"] : string.Format(_text.Culture, _text["MeasureStep"], FineStep.ToString("0.##", _text.Culture), UnitLabel);
    public string DecreaseLabel => string.Format(_text.Culture, _text["MeasureDecrease"], FineStepText);
    public string IncreaseLabel => string.Format(_text.Culture, _text["MeasureIncrease"], FineStepText);
    public string ExactText => DisplayValue is { } value ? value.ToString("0.##", _text.Culture) : string.Empty;
    public string? ValidationMessage { get; private set; }
    public string UnitLabel => IsWeight ? _text[_weightUnit == WeightUnitEnum.Lb ? "MeasureUnitLb" : "MeasureUnitKg"] : _text[_lengthUnit == LengthUnitEnum.In ? "MeasureUnitIn" : "MeasureUnitCm"];

    private decimal? DisplayValue => IsWeight
        ? _measurement.ToDisplayWeight(_canonicalValue, _weightUnit)
        : _measurement.ToDisplayLength(_canonicalValue, _lengthUnit);

    public event PropertyChangedEventHandler? PropertyChanged;

    public static decimal SliderStepFor(MeasurementKindEnum kind, WeightUnitEnum weightUnit, LengthUnitEnum lengthUnit)
    {
        if (kind == MeasurementKindEnum.Weight)
        {
            return weightUnit == WeightUnitEnum.Lb ? 0.25m : 0.1m;
        }

        return lengthUnit == LengthUnitEnum.In ? 0.5m : 1m;
    }

    public static decimal FineStepFor(MeasurementKindEnum kind, WeightUnitEnum weightUnit, LengthUnitEnum lengthUnit)
    {
        if (kind == MeasurementKindEnum.Weight)
        {
            return weightUnit == WeightUnitEnum.Lb ? 1m / OuncesPerPound : 0.1m;
        }

        return lengthUnit == LengthUnitEnum.In ? 0.25m : 1m;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _text.PropertyChanged -= OnCultureChanged;
    }

    public void SelectRange(MeasurementScaleRangeModel range)
    {
        if (!range.CanDisplay(_canonicalValue))
        {
            return;
        }

        _range = range;
        Notify();
    }

    public void SetUnits(WeightUnitEnum weightUnit, LengthUnitEnum lengthUnit)
    {
        _weightUnit = weightUnit;
        _lengthUnit = lengthUnit;
        _hasInvalidInput = false;
        SynchroniseInputs();
        Validate();
        Notify();
    }

    public void SetSliderValue(decimal displayValue)
    {
        _suppressEntryCommit = true;
        var step = SliderStep;
        var snapped = Math.Round(displayValue / step, MidpointRounding.AwayFromZero) * step;
        SetDisplayValue(Math.Clamp(snapped, 0m, SliderMaximum));
    }

    public void SetSliderFraction(decimal fraction)
    {
        SetSliderValue(Math.Clamp(fraction, 0m, 1m) * SliderMaximum);
    }

    public void SetExactText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Clear();
            return;
        }

        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, _text.Culture, out var parsed))
        {
            _hasInvalidInput = true;
            ValidationMessage = _text["MeasureInvalid"];
            Notify();
            return;
        }

        SetDisplayValue(parsed);
    }

    public void SetPounds(int value)
    {
        _pounds = Math.Max(0, value);
        SetImperialWeight();
    }

    public void SetOunces(int value)
    {
        _ounces = Math.Clamp(value, 0, OuncesPerPound - 1);
        SetImperialWeight();
    }

    public void Increase(
        string? pendingExactText = null,
        string? pendingPoundsText = null,
        string? pendingOuncesText = null)
    {
        CommitPending(pendingExactText, pendingPoundsText, pendingOuncesText);
        if (IsImperialWeight)
        {
            SetImperialOunces((_pounds * OuncesPerPound) + _ounces + 1);
            _suppressEntryCommit = true;
            return;
        }

        SetDisplayValue((DisplayValue ?? 0m) + FineStep);
        _suppressEntryCommit = true;
    }

    public void Decrease(
        string? pendingExactText = null,
        string? pendingPoundsText = null,
        string? pendingOuncesText = null)
    {
        CommitPending(pendingExactText, pendingPoundsText, pendingOuncesText);
        if (IsImperialWeight)
        {
            SetImperialOunces(Math.Max(0, (_pounds * OuncesPerPound) + _ounces - 1));
            _suppressEntryCommit = true;
            return;
        }

        SetDisplayValue(Math.Max(0m, (DisplayValue ?? 0m) - FineStep));
        _suppressEntryCommit = true;
    }

    public void Clear()
    {
        _canonicalValue = null;
        _hasInvalidInput = false;
        ValidationMessage = null;
        _suppressEntryCommit = true;
        SynchroniseInputs();
        Notify();
    }

    public decimal? Apply()
    {
        Validate();
        Notify();
        return IsValid ? _canonicalValue : null;
    }

    public void MarkEntryEdited()
    {
        _suppressEntryCommit = false;
    }

    public MeasurementEditorApplyResult TryApply(
        string? pendingExactText = null,
        string? pendingPoundsText = null,
        string? pendingOuncesText = null)
    {
        CommitInputUnlessSuperseded(pendingExactText, pendingPoundsText, pendingOuncesText);
        Apply();
        if (!IsValid)
        {
            return MeasurementEditorApplyResult.Invalid(ValidationMessage ?? _text["MeasureInvalid"]);
        }

        return MeasurementEditorApplyResult.Applied(_canonicalValue);
    }

    public void CommitPending(
        string? pendingExactText = null,
        string? pendingPoundsText = null,
        string? pendingOuncesText = null)
    {
        if (IsImperialWeight)
        {
            if (int.TryParse(pendingPoundsText, out var pounds))
            {
                SetPounds(pounds);
            }

            if (int.TryParse(pendingOuncesText, out var ounces))
            {
                SetOunces(ounces);
            }

            return;
        }

        if (pendingExactText is not null)
        {
            SetExactText(pendingExactText);
        }
    }

    public void CommitInputUnlessSuperseded(
        string? pendingExactText = null,
        string? pendingPoundsText = null,
        string? pendingOuncesText = null)
    {
        if (_suppressEntryCommit)
        {
            return;
        }

        CommitPending(pendingExactText, pendingPoundsText, pendingOuncesText);
    }

    private void SetImperialOunces(int totalOunces)
    {
        _pounds = totalOunces / OuncesPerPound;
        _ounces = totalOunces % OuncesPerPound;
        SetImperialWeight();
    }

    private void SetImperialWeight()
    {
        _hasInvalidInput = false;
        _canonicalValue = _pounds == 0 && _ounces == 0 ? null : _measurement.FromPoundsAndOunces(_pounds, _ounces);
        Validate();
        Notify();
    }

    private void SetDisplayValue(decimal displayValue)
    {
        _hasInvalidInput = false;
        _canonicalValue = IsWeight
            ? _measurement.ToCanonicalWeight(displayValue, _weightUnit, _canonicalValue)
            : _measurement.ToCanonicalLength(displayValue, _lengthUnit, _canonicalValue);
        SynchroniseInputs();
        Validate();
        Notify();
    }

    private void Validate()
    {
        _range = MeasurementScaleRangeModel.ExpandedFor(IsWeight, _range, _canonicalValue);
        var valid = IsWeight ? CatchDetailConstants.IsWeightValid(_canonicalValue) : CatchDetailConstants.IsLengthValid(_canonicalValue);
        ValidationMessage = valid ? null : _text["MeasureInvalid"];
    }

    private void SynchroniseInputs()
    {
        if (IsImperialWeight)
        {
            (_pounds, _ounces) = _measurement.ToPoundsAndOunces(_canonicalValue);
        }
    }

    private decimal DisplayOf(decimal canonicalValue)
    {
        var display = IsWeight
            ? _measurement.ToDisplayWeight(canonicalValue, _weightUnit)
            : _measurement.ToDisplayLength(canonicalValue, _lengthUnit);
        return display ?? canonicalValue;
    }

    private string FormatCanonical(decimal canonicalValue)
    {
        if (IsWeight)
        {
            var unit = _text[_weightUnit == WeightUnitEnum.Lb ? "MeasureUnitLb" : "MeasureUnitKg"];
            return _measurement.FormatWeight(canonicalValue, _weightUnit, unit, _text["MeasureUnitOz"]);
        }

        return _measurement.FormatLength(canonicalValue, _lengthUnit, UnitLabel);
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UiTextService.Culture))
        {
            if (_hasInvalidInput)
            {
                ValidationMessage = _text["MeasureInvalid"];
            }
            else
            {
                Validate();
            }

            Notify();
        }
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
