using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.CatchPhotographProposalServiceTests;

public class BaseCatchPhotographProposalServiceTest
{
    protected static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-25T12:00:00Z");

    protected readonly CatchPhotographProposalService Sut = new();

    protected static PhotographMetadataModel Dated(string capturedOn)
    {
        return new PhotographMetadataModel(
            DateTimeOffset.Parse(capturedOn),
            null,
            null,
            PhotographCapturedOnSourceEnum.ExifOriginal);
    }

    protected static PhotographMetadataModel FileDated(string lastModified)
    {
        return new PhotographMetadataModel(
            DateTimeOffset.Parse(lastModified),
            null,
            null,
            PhotographCapturedOnSourceEnum.FileLastModified);
    }

    protected static PhotographMetadataModel Located(double latitude, double longitude)
    {
        return new PhotographMetadataModel(null, latitude, longitude);
    }

    protected static PhotographMetadataModel DatedAndLocated(string capturedOn, double latitude, double longitude)
    {
        return new PhotographMetadataModel(
            DateTimeOffset.Parse(capturedOn),
            latitude,
            longitude,
            PhotographCapturedOnSourceEnum.ExifOriginal);
    }
}
