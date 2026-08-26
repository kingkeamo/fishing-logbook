namespace FishingLogBook.Web.Features.Photographs.Models;

public sealed record PhotographCarouselItemModel(
    Guid Id,
    string ContentType,
    byte[]? Bytes = null,
    string? RemoteUrl = null);
