namespace FishingLogBook.Shared.Dtos;

public sealed class ClientDiagnosticBatchDto
{
    public IReadOnlyList<ClientDiagnosticEventDto> Events { get; set; } = [];
}
