namespace FishingLogBook.Web.Common.Modals;

public sealed record MessageModalModel(
    string Title,
    string Message,
    string CloseText,
    ModalSeverity Severity = ModalSeverity.Information);
