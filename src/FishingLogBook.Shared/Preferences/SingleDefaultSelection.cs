namespace FishingLogBook.Shared.Preferences;

public static class SingleDefaultSelection
{
    public static Guid? Ensure(IReadOnlyCollection<Guid> selectedIds, Guid? defaultId)
    {
        ArgumentNullException.ThrowIfNull(selectedIds);

        if (defaultId.HasValue && selectedIds.Contains(defaultId.Value))
        {
            return defaultId;
        }

        return selectedIds.Count == 0 ? null : selectedIds.First();
    }

    public static Guid Select(IReadOnlyCollection<Guid> selectedIds, Guid defaultId)
    {
        ArgumentNullException.ThrowIfNull(selectedIds);

        if (!selectedIds.Contains(defaultId))
        {
            throw new ArgumentOutOfRangeException(nameof(defaultId));
        }

        return defaultId;
    }
}
