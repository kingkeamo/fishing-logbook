using FishingLogBook.Shared.Catches.Models;
using FishingLogBook.Shared.Photographs.Models;

namespace FishingLogBook.Shared.Catches.Services;

public interface ICatchPhotographProposalService
{
    CatchPhotographProposalModel Propose(IReadOnlyList<PhotographMetadataModel> photographs, DateTimeOffset now);
}
