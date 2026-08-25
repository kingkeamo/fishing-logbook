using FishingLogBook.Web.Features.Catch.Enums;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Services;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.PhotoMetadataProposalServiceTests;

public class BasePhotoMetadataProposalServiceTest
{
    protected static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-25T12:00:00Z");

    protected readonly PhotoMetadataProposalService Sut = new();

    protected static PhotoMetadataModel Dated(string capturedOn)
    {
        return new PhotoMetadataModel(
            DateTimeOffset.Parse(capturedOn),
            null,
            null,
            PhotoCapturedOnSourceEnum.ExifOriginal);
    }

    protected static PhotoMetadataModel FileDated(string lastModified)
    {
        return new PhotoMetadataModel(
            DateTimeOffset.Parse(lastModified),
            null,
            null,
            PhotoCapturedOnSourceEnum.FileLastModified);
    }

    protected static PhotoMetadataModel Located(double latitude, double longitude)
    {
        return new PhotoMetadataModel(null, latitude, longitude);
    }

    protected static PhotoMetadataModel DatedAndLocated(string capturedOn, double latitude, double longitude)
    {
        return new PhotoMetadataModel(
            DateTimeOffset.Parse(capturedOn),
            latitude,
            longitude,
            PhotoCapturedOnSourceEnum.ExifOriginal);
    }
}
