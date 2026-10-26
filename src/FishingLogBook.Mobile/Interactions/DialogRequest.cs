namespace FishingLogBook.Mobile.Interactions;

public sealed record DialogRequest(string Title, string Message, string Accept, string Cancel, bool IsDestructive = false);
