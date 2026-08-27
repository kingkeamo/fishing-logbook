namespace FishingLogBook.Shared.Constants;

public static class TripConstants
{
    public const int MaxTitleLength = 120;

    public const int MaxPlaceNameLength = 160;

    public const int MaxNoteTextLength = CatchDetailConstants.MaxNotesLength;

    public const string Active = "Active";

    public const string Completed = "Completed";

    public static string? TrimPlaceName(string? placeName)
    {
        var trimmed = placeName?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxPlaceNameLength)
        {
            return null;
        }

        return trimmed;
    }

    public static string? TrimTitle(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxTitleLength)
        {
            return null;
        }

        return trimmed;
    }

    public static bool IsNoteTextValid(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) && text.Trim().Length <= MaxNoteTextLength;
    }

    public static string? TrimNoteText(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

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
