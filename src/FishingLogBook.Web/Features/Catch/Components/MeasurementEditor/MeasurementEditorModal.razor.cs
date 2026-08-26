using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;

public partial class MeasurementEditorModal : ComponentBase
{
    private MeasurementScaleRangeModel _scaleRange = MeasurementScaleRangeModel.Weights[0];
    private decimal? _canonicalValue;
    private string? _validationMessage;
    private int _pounds;
    private int _ounces;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public MeasurementEditorModel Model { get; set; } = default!;

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    protected override void OnParametersSet()
    {
        _canonicalValue = Model.CanonicalValue;
        _scaleRange = MeasurementScaleRangeModel.SmallestFor(Model.IsWeight, _canonicalValue);
        SynchroniseInputs();
    }

    private bool IsImperialWeight => Model.IsWeight && Model.WeightUnit == WeightUnitEnum.Lb;

    private string Title => Model.IsWeight ? Loc["Catch_EditWeight"] : Loc["Catch_EditLength"];

    private string ExactLabel
    {
        get
        {
            if (Model.IsWeight)
            {
                return Loc["Catch_MeasurementExactWithUnit", Loc["Catch_WeightUnitShort_Kg"]];
            }

            var unit = Model.LengthUnit == LengthUnitEnum.In
                ? Loc["Catch_LengthUnitShort_In"]
                : Loc["Catch_LengthUnitShort_Cm"];
            return Loc["Catch_MeasurementExactWithUnit", unit];
        }
    }

    private decimal SliderValue => Math.Clamp(DisplayValue ?? 0m, 0m, SliderMaximum);

    private static IEnumerable<int> DialTickAngles => Enumerable.Range(0, 24).Select(index => index * 15);

    private static IEnumerable<decimal> TapeTickPositions => Enumerable.Range(0, 18).Select(index => 56m + (index * 10m));

    private decimal DialAngle => Math.Round(SliderValue / SliderMaximum * 359m, 2);

    private decimal TapeWidth => 12m + (SliderValue / SliderMaximum * 166m);

    private string TapeHookPath
    {
        get
        {
            var end = 50m + TapeWidth;
            return FormattableString.Invariant($"M {end} 24 v 45 h 8 v -8 h -3 v -29 h 3 v -8 z");
        }
    }

    private decimal SliderMaximum
    {
        get
        {
            return DisplayOf(_scaleRange.MaximumCanonical);
        }
    }

    private IReadOnlyList<MeasurementScaleRangeModel> ScaleRanges =>
        MeasurementScaleRangeModel.For(Model.IsWeight);

    private string SliderMinimumLabel => FormatCanonicalForDisplay(0m);

    private string SliderMaximumLabel => FormatCanonicalForDisplay(_scaleRange.MaximumCanonical);

    private decimal DisplayOf(decimal canonicalValue)
    {
        var display = Model.IsWeight
            ? Measurement.ToDisplayWeight(canonicalValue, Model.WeightUnit)
            : Measurement.ToDisplayLength(canonicalValue, Model.LengthUnit);
        return display ?? canonicalValue;
    }

    private string FormatCanonicalForDisplay(decimal canonicalValue)
    {
        if (Model.IsWeight)
        {
            return Measurement.FormatWeight(
                canonicalValue,
                Model.WeightUnit,
                Model.WeightUnit == WeightUnitEnum.Lb
                    ? Loc["Catch_WeightUnitShort_Lb"]
                    : Loc["Catch_WeightUnitShort_Kg"],
                Loc["Catch_WeightUnitShort_Oz"]);
        }

        return Measurement.FormatLength(
            canonicalValue,
            Model.LengthUnit,
            Model.LengthUnit == LengthUnitEnum.In
                ? Loc["Catch_LengthUnitShort_In"]
                : Loc["Catch_LengthUnitShort_Cm"]);
    }

    private void SelectScaleRange(MeasurementScaleRangeModel range)
    {
        if (!range.CanDisplay(_canonicalValue))
        {
            return;
        }

        _scaleRange = range;
    }

    private void ExpandScaleRangeForValue()
    {
        _scaleRange = MeasurementScaleRangeModel.ExpandedFor(
            Model.IsWeight,
            _scaleRange,
            _canonicalValue);
    }

    private decimal SliderStep
    {
        get
        {
            if (Model.IsWeight)
            {
                return Model.WeightUnit == WeightUnitEnum.Lb ? 0.25m : 0.1m;
            }

            return Model.LengthUnit == LengthUnitEnum.In ? 0.5m : 1m;
        }
    }

    private decimal FineStep
    {
        get
        {
            if (Model.IsWeight)
            {
                return Model.WeightUnit == WeightUnitEnum.Lb ? 1m / 16m : 0.1m;
            }

            return Model.LengthUnit == LengthUnitEnum.In ? 0.25m : 1m;
        }
    }

    private string FineStepDisplay
    {
        get
        {
            if (IsImperialWeight)
            {
                return Loc["Catch_MeasurementOneOunceStep"];
            }

            var unit = Model.IsWeight
                ? Model.WeightUnit == WeightUnitEnum.Lb
                    ? Loc["Catch_WeightUnitShort_Lb"]
                    : Loc["Catch_WeightUnitShort_Kg"]
                : Model.LengthUnit == LengthUnitEnum.In
                    ? Loc["Catch_LengthUnitShort_In"]
                    : Loc["Catch_LengthUnitShort_Cm"];
            return Loc["Catch_MeasurementStep", FineStep, unit];
        }
    }

    private string DecreaseLabel => Loc["Catch_MeasurementDecrease", FineStepDisplay];

    private string IncreaseLabel => Loc["Catch_MeasurementIncrease", FineStepDisplay];

    private decimal? DisplayValue => Model.IsWeight
        ? Measurement.ToDisplayWeight(_canonicalValue, Model.WeightUnit)
        : Measurement.ToDisplayLength(_canonicalValue, Model.LengthUnit);

    private void SetExactDisplayValue(decimal? displayValue)
    {
        if (displayValue is null)
        {
            return;
        }

        SetDisplayValue(displayValue);
    }

    private void SetSliderValue(decimal value)
    {
        SetDisplayValue(value);
    }

    private void SetPounds(int value)
    {
        _pounds = Math.Max(0, value);
        SetImperialWeight();
    }

    private void SetOunces(int value)
    {
        _ounces = Math.Clamp(value, 0, 15);
        SetImperialWeight();
    }

    private void SetImperialWeight()
    {
        _canonicalValue = _pounds == 0 && _ounces == 0
            ? null
            : Measurement.FromPoundsAndOunces(_pounds, _ounces);
        ValidateCanonicalValue();
    }

    private void Increase()
    {
        if (IsImperialWeight)
        {
            SetImperialOunces((_pounds * 16) + _ounces + 1);
            return;
        }

        SetDisplayValue((DisplayValue ?? 0m) + FineStep);
    }

    private void Decrease()
    {
        if (IsImperialWeight)
        {
            SetImperialOunces(Math.Max(0, (_pounds * 16) + _ounces - 1));
            return;
        }

        SetDisplayValue(Math.Max(0m, (DisplayValue ?? 0m) - FineStep));
    }

    private void SetImperialOunces(int totalOunces)
    {
        _pounds = totalOunces / 16;
        _ounces = totalOunces % 16;
        SetImperialWeight();
    }

    private void SetDisplayValue(decimal? displayValue)
    {
        _canonicalValue = Model.IsWeight
            ? Measurement.ToCanonicalWeight(displayValue, Model.WeightUnit, _canonicalValue)
            : Measurement.ToCanonicalLength(displayValue, Model.LengthUnit, _canonicalValue);
        ValidateCanonicalValue();
        SynchroniseInputs();
    }

    private void ValidateCanonicalValue()
    {
        ExpandScaleRangeForValue();
        var valid = Model.IsWeight
            ? CatchDetailConstants.IsWeightValid(_canonicalValue)
            : CatchDetailConstants.IsLengthValid(_canonicalValue);
        _validationMessage = valid ? null : Loc["Catch_MeasurementInvalid"].Value;
    }

    private void SynchroniseInputs()
    {
        if (IsImperialWeight)
        {
            (_pounds, _ounces) = Measurement.ToPoundsAndOunces(_canonicalValue);
        }
    }

    private void Apply()
    {
        ValidateCanonicalValue();
        if (_validationMessage is not null)
        {
            return;
        }

        MudDialog.Close(DialogResult.Ok(new MeasurementEditorResult(_canonicalValue)));
    }

    private void Clear()
    {
        _canonicalValue = null;
        _validationMessage = null;
        SynchroniseInputs();
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private static bool IsMajorDialTick(int angle)
    {
        return angle % 45 == 0;
    }

    private static bool IsMajorTapeTick(decimal position)
    {
        return (position - 56m) % 50m == 0;
    }

}
