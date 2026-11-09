using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Shared.Loaders;

public static class LoaderKindResolver
{
    private static readonly LoaderKindEnum[] Designs =
    [
        LoaderKindEnum.LeapingFish,
        LoaderKindEnum.Boat,
        LoaderKindEnum.Bobber
    ];

    public static IReadOnlyList<LoaderKindEnum> AllDesigns => Designs;

    public static LoaderKindEnum Resolve(LoaderKindEnum requested, Func<int, int>? nextIndex = null)
    {
        if (requested != LoaderKindEnum.Random)
        {
            return requested;
        }

        var index = (nextIndex ?? System.Random.Shared.Next)(Designs.Length);

        return Designs[Math.Clamp(index, 0, Designs.Length - 1)];
    }
}
