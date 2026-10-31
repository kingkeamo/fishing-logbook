using FishingLogBook.Mobile.Features.Catch.Enums;

namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed class CatchHistoryModel
{
    public static readonly CatchHistoryModel None = new([], RemoteCatchStateEnum.NotSignedIn);

    public CatchHistoryModel(IReadOnlyList<CatchPresentationModel> items, RemoteCatchStateEnum remote)
    {
        Items = items;
        Remote = remote;
    }

    public IReadOnlyList<CatchPresentationModel> Items { get; }

    public RemoteCatchStateEnum Remote { get; }

    public bool ServerHistoryLoaded => Remote == RemoteCatchStateEnum.Loaded;
}
