namespace FishingLogBook.Domain.Catches;

public sealed class Catch
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public DateTimeOffset CaughtOn { get; init; }

    public IReadOnlyList<CatchPhotograph> Photographs { get; init; } = [];
}
