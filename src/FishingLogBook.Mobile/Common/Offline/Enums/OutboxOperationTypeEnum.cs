namespace FishingLogBook.Mobile.Common.Offline.Enums;

public enum OutboxOperationTypeEnum
{
    UpsertCatchMetadata = 0,
    DeleteCatch = 1,
    UploadCatchPhotograph = 2,
    UpsertTripMetadata = 3,
    WriteTripNote = 4,
    WriteTripPhotograph = 5
}
