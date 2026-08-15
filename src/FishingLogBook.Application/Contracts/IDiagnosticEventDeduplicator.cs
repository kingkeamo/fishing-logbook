namespace FishingLogBook.Application.Contracts;

public interface IDiagnosticEventDeduplicator
{
    bool TryAccept(Guid id);
}
