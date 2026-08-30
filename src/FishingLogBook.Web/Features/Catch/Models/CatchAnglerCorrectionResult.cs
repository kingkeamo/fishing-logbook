using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record CatchAnglerCorrectionResult(CatchViewDto? Catch, string? ErrorMessage);
