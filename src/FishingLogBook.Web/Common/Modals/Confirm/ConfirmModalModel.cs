namespace FishingLogBook.Web.Common.Modals;

public sealed record ConfirmModalModel(
    string Title,
    string Message,
    string ConfirmText,
    string CancelText,
    bool IsDestructive = false);
