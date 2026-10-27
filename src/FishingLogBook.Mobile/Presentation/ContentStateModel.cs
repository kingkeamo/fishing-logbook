using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Presentation;

public sealed class ContentStateModel(ContentStateEnum state, UiTextService text, string? textKey = null)
{
    public ContentStateEnum State { get; } = state;
    public bool IsLoading => State == ContentStateEnum.Loading;
    public bool IsVisible => State != ContentStateEnum.Ready;
    public bool CanRetry => State is ContentStateEnum.Error or ContentStateEnum.Offline;
    public bool IsEmpty => State == ContentStateEnum.Empty;
    public string Title => text[(textKey ?? State.ToString()) + "Title"];
    public string Message => text[(textKey ?? State.ToString()) + "Body"];
    public string Retry => text["Retry"];
    public string Action => text["RecordFirstCatch"];
}
