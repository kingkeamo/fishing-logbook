namespace FishingLogBook.Application.Contracts;

public sealed record DatabaseMigrationResult(bool Successful, IReadOnlyList<string> ScriptsExecuted, string? Error);
