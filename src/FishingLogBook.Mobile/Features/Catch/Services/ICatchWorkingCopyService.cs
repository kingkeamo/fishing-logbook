using FishingLogBook.Mobile.Features.Catch.Enums;

namespace FishingLogBook.Mobile.Features.Catch.Services;

public interface ICatchWorkingCopyService
{
    Task<WorkingCopyOutcomeEnum> EnsureAsync(Guid catchId, CancellationToken cancellationToken);
}
