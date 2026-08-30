namespace FishingLogBook.Application.Args;

public sealed class FindAnglersArgs
{
    public Guid RequestingUserId { get; init; }

    public string Query { get; init; } = string.Empty;

    public int MaxResults { get; init; }
}
