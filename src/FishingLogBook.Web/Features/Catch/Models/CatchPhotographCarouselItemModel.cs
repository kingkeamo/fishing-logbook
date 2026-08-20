namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record CatchPhotographCarouselItemModel(
    Guid Id,
    string ContentType,
    byte[]? Bytes = null,
    string? RemoteUrl = null);
