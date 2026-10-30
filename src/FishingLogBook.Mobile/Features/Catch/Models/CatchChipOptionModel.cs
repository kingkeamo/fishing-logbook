namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed record CatchChipOptionModel(string Code, string Name, bool IsSelected = false)
{
    public static CatchChipOptionModel FromValue(string value, bool isSelected = false)
    {
        return new CatchChipOptionModel(new string([.. value.Where(char.IsLetterOrDigit)]), value, isSelected);
    }

    public static IReadOnlyList<CatchChipOptionModel> BuildShortlist(
        IReadOnlyList<CatchChipOptionModel> options,
        string currentValue,
        int maximum)
    {
        var current = currentValue.Trim();
        var shortlist = options.Take(maximum).ToList();
        if (current.Length > 0
            && !shortlist.Any(option => string.Equals(option.Name, current, StringComparison.OrdinalIgnoreCase)))
        {
            shortlist.Insert(0, FromValue(current, isSelected: true));
        }

        return shortlist
            .Select(option => option with
            {
                IsSelected = current.Length > 0
                    && string.Equals(option.Name, current, StringComparison.OrdinalIgnoreCase)
            })
            .ToArray();
    }
}
