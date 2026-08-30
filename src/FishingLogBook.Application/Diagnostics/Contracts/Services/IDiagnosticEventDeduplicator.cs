namespace FishingLogBook.Application.Diagnostics.Contracts.Services;

public interface IDiagnosticEventDeduplicator
{
    bool TryAccept(Guid id);
}
