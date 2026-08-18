namespace FishingLogBook.Shared.Constants;

public static class CatchDetailConstants
{
    public const decimal MaxWeightKilograms = 1000m;

    public const decimal MaxLengthCentimetres = 1000m;

    public const int MaxSpeciesNameLength = 100;

    public const int MaxMethodLength = 100;

    public const int MaxBaitOrLureLength = 100;

    public const int MaxNotesLength = 2000;

    public static readonly TimeSpan MaxCaughtOnFutureSkew = TimeSpan.FromMinutes(15);

    public static bool IsCaughtOnValid(DateTimeOffset caughtOn, DateTimeOffset now)
    {
        return caughtOn != default && caughtOn <= now + MaxCaughtOnFutureSkew;
    }

    public static bool IsWeightValid(decimal? weight)
    {
        return weight is null || (weight > 0 && weight <= MaxWeightKilograms);
    }

    public static bool IsLengthValid(decimal? length)
    {
        return length is null || (length > 0 && length <= MaxLengthCentimetres);
    }

    public static bool IsOptionalTextValid(string? value, int maxLength)
    {
        return value is null || value.Length <= maxLength;
    }
}
