using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed class CatchGroupModel(DateOnly day, IEnumerable<CatchPresentationModel> catches, UiTextService text) : List<CatchPresentationModel>(catches)
{
    public DateOnly Day { get; } = day;
    public string Header => Day.ToString("dddd d MMMM yyyy", text.Culture);
    public string CountText => string.Format(text.Culture, text[Count == 1 ? "CatchCountSingle" : "CatchCount"], Count);
}
