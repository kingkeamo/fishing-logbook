namespace FishingLogBook.Mobile.Features.Profile.Models;

public sealed class MeasurementUnitOptionModel<T>(T value, string label)
    where T : struct, Enum
{
    public T Value { get; } = value;
    public string Label { get; } = label;
}
