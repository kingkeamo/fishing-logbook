using FishingLogBook.Mobile.IO.Enums;

namespace FishingLogBook.Mobile.IO.Models;

public sealed record DeviceGpsReadModel(
    DeviceGpsReadOutcomeEnum Outcome,
    DeviceGpsFixModel? Fix)
{
    public static DeviceGpsReadModel Captured(DeviceGpsFixModel fix)
    {
        return new(DeviceGpsReadOutcomeEnum.Captured, fix);
    }

    public static DeviceGpsReadModel Failed(DeviceGpsReadOutcomeEnum outcome)
    {
        return new(outcome, null);
    }
}
