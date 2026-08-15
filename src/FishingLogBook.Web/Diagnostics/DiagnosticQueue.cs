namespace FishingLogBook.Web.Diagnostics;

public static class DiagnosticQueue
{
    public static void TrimOldest(IList<DiagnosticEvent> items, int maxQueueSize)
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
