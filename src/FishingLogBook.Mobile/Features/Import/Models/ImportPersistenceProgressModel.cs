using FishingLogBook.Mobile.Features.Import.Enums;

namespace FishingLogBook.Mobile.Features.Import.Models;

public sealed record ImportPersistenceProgressModel(
    ImportPersistenceStageEnum Stage,
    int Current,
    int Total);
