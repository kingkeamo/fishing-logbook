namespace FishingLogBook.Web.Features.Import.Enums;

public enum ImportCatchProposalReasonEnum
{
    TrustworthyCaptureTime = 0,
    MissingTimestamp = 1,
    AmbiguousTimestamp = 2,
    WeakTimestamp = 3,
    UnusableTimestamp = 4,
    ConflictingGps = 5
}
