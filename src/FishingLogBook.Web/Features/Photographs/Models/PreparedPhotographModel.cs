using FishingLogBook.Web.Features.Photographs.Enums;

namespace FishingLogBook.Web.Features.Photographs.Models;

public sealed record PreparedPhotographModel(
    Guid Id,
    string ContentType,
    byte[] Bytes,
    PhotographSourceEnum Source,
    PhotographMetadataModel Metadata,
    string? CapturedOnLocal)
{
    public bool FromCamera
    {
        get
        {
            return Source == PhotographSourceEnum.Camera;
        }
    }

    public PhotographCarouselItemModel ToCarouselItem()
    {
        return new PhotographCarouselItemModel(Id, ContentType, Bytes);
    }
}
