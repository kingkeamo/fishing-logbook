namespace FishingLogBook.Shared.Constants;

public static class TripParticipantConstants
{
    public const string Pending = "Pending";

    public const string Accepted = "Accepted";

    public const string Declined = "Declined";

    public const string Owner = "Owner";

    public const string Participant = "Participant";

    public const string None = "None";

    public static bool IsKnownStatus(string? status)
    {
        return status is Pending or Accepted or Declined;
    }

    public static bool IsKnownRole(string? role)
    {
        return role is Owner or Participant or None;
    }
}
