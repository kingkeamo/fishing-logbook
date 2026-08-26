namespace FishingLogBook.Shared.Constants;

public static class TripConstants
{
    public const int MaxTitleLength = 120;

    public const int MaxPlaceNameLength = 160;

    public const string Active = "Active";

    public const string Completed = "Completed";

    public static bool IsKnownStatus(string? status)
    {
        return status is Active or Completed;
    }

    public static bool IsStartedOnValid(DateTimeOffset startedOn, DateTimeOffset now)
    {
        return startedOn != default && startedOn <= now;
    }

    public static bool IsEndedOnValid(DateTimeOffset startedOn, DateTimeOffset? endedOn, DateTimeOffset now)
    {
        if (endedOn is null)
        {
            return true;
        }

        return endedOn.Value >= startedOn && endedOn.Value <= now;
    }
}
