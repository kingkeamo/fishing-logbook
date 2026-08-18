namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record CatchChipOptionModel(string Code, string Name)
{
    public static CatchChipOptionModel FromValue(string value)
    {
        return new CatchChipOptionModel(new string([.. value.Where(char.IsLetterOrDigit)]), value);
    }

    public static IReadOnlyList<CatchChipOptionModel> BuildShortlist(
        IReadOnlyList<CatchChipOptionModel> options,
        string currentValue,
        int maximum)
    {
        var shortlist = options.Take(maximum).ToList();
        var current = currentValue.Trim();
        if (current.Length == 0
            || shortlist.Any(option => string.Equals(option.Name, current, StringComparison.OrdinalIgnoreCase)))
        {
            return shortlist;
        }

        shortlist.Insert(0, FromValue(current));
        return shortlist;
    }
}
