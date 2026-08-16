using FishingLogBook.Web.Features.Diagnostics.Models;
namespace FishingLogBook.Web.Features.Diagnostics.Storage;

public static class DiagnosticQueue
{
    public static void TrimOldest(IList<DiagnosticEventModel> items, int maxQueueSize)
    {
        if (maxQueueSize <= 0 || items.Count <= maxQueueSize)
        {
            return;
        }

        var overflow = items.Count - maxQueueSize;
        var oldest = items.OrderBy(item => item.TimestampUtc).ThenBy(item => item.Id).Take(overflow).Select(item => item.Id).ToHashSet();
        for (var index = items.Count - 1; index >= 0; index--)
        {
            if (oldest.Contains(items[index].Id))
            {
                items.RemoveAt(index);
            }
        }
    }
}
