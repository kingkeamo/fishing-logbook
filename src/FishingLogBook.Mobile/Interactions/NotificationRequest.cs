namespace FishingLogBook.Mobile.Interactions;

public sealed record NotificationRequest(NotificationKindEnum Kind, string Message, string Dismiss);
