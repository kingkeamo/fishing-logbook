using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Photographs.Models;

namespace FishingLogBook.Web.Features.Catch.Services;

public interface ICatchPhotographProposalService
{
    CatchPhotographProposalModel Propose(IReadOnlyList<PhotographMetadataModel> photographs, DateTimeOffset now);
}
