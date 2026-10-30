using FishingLogBook.Mobile.Common.Offline.Enums;

namespace FishingLogBook.Mobile.Common.Offline.Models;

public sealed class OutboxOperationModel
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public required OutboxAggregateTypeEnum AggregateType { get; init; }

    public required Guid AggregateId { get; init; }

    public required OutboxOperationTypeEnum OperationType { get; init; }

    public required string PayloadJson { get; init; }

    public required OutboxStatusEnum Status { get; init; }

    public required int AttemptCount { get; init; }

    public required DateTimeOffset CreatedOn { get; init; }

    public required DateTimeOffset AvailableAt { get; init; }

    public DateTimeOffset? LastAttemptAt { get; init; }

    public string? LastError { get; init; }
}
