namespace FishingLogBook.Web.Diagnostics;

public sealed class CorrelationContext
{
    public Guid CorrelationId { get; private set; } = Guid.NewGuid();

    public Guid StartNew()
    {
        CorrelationId = Guid.NewGuid();
        return CorrelationId;
    }
}
