using FishingLogBook.Web.Features.Import.Enums;

namespace FishingLogBook.Web.Features.Import.Models;

public sealed record ImportPersistenceProgressModel(
    ImportPersistenceStageEnum Stage,
    int Current,
    int Total);
