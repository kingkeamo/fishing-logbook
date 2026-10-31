using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Common.Offline.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;

namespace FishingLogBook.Mobile.Common.Offline.Services;

public interface IOutboxOperationSynchroniser
{
    OutboxOperationTypeEnum OperationType { get; }

    Task<OutboxOperationResultModel> SynchroniseAsync(
        OutboxOperationModel operation,
        AccountSessionLeaseModel lease,
        CancellationToken cancellationToken);
}
