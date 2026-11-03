namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record ImportMergeTargetModel(Guid SourceCatchId, Guid CatchProposalId, string Label);
