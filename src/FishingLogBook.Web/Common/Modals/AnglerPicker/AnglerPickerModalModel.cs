namespace FishingLogBook.Web.Common.Modals.AnglerPicker;

public sealed record AnglerPickerModalModel(IReadOnlyCollection<Guid>? ExcludedUserIds = null)
{
    public IReadOnlyCollection<Guid> ExcludedUserIds { get; init; } = ExcludedUserIds ?? [];
}
