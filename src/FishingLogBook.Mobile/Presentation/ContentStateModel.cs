using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Presentation;

public sealed class ContentStateModel(ContentStateEnum state, UiTextService text)
{
    public ContentStateEnum State { get; } = state;
    public bool IsLoading => State == ContentStateEnum.Loading;
    public bool IsVisible => State != ContentStateEnum.Ready;
    public bool CanRetry => State is ContentStateEnum.Error or ContentStateEnum.Offline;
    public bool IsEmpty => State == ContentStateEnum.Empty;
    public string Title => text[State + "Title"];
    public string Message => text[State + "Body"];
    public string Retry => text["Retry"];
    public string Action => text["RecordFirstCatch"];
}
