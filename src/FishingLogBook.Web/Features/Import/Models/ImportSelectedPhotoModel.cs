using FishingLogBook.Web.Features.Import.Enums;

namespace FishingLogBook.Web.Features.Import.Models;

public sealed class ImportSelectedPhotoModel
{
    public ImportSelectedPhotoModel(
        Guid id,
        int selectionIndex,
        string contentType,
        long byteSize,
        string blobToken,
        string? fileName = null,
        string? thumbnailUrl = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A photo identity is required.", nameof(id));
        }

        if (selectionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectionIndex));
        }

        Id = id;
        SelectionIndex = selectionIndex;
        ContentType = contentType;
        ByteSize = byteSize;
        BlobToken = blobToken;
        FileName = fileName;
        ThumbnailUrl = thumbnailUrl;
    }

    public Guid Id { get; }

    public int SelectionIndex { get; }

    public string? FileName { get; }

    public string ContentType { get; }

    public long ByteSize { get; }

    public string BlobToken { get; }

    public string? ThumbnailUrl { get; private set; }

    public ImportMetadataStatusEnum MetadataStatus { get; private set; }

    public string? MetadataError { get; private set; }

    public ImportTimestampModel Timestamp { get; private set; } = ImportTimestampModel.Missing();

    public ImportLocationModel Location { get; private set; } = new(null, null, false);

    public ImportDuplicateStatusEnum DuplicateStatus { get; private set; }

    public string? Fingerprint { get; private set; }

    public bool IsRemoved { get; private set; }

    public void SetMetadata(
        ImportMetadataStatusEnum status,
        ImportTimestampModel timestamp,
        ImportLocationModel location,
        string? error = null)
    {
        MetadataStatus = status;
        Timestamp = timestamp;
        Location = location;
        MetadataError = error;
    }

    public void SetDuplicateState(ImportDuplicateStatusEnum status, string? fingerprint)
    {
        DuplicateStatus = status;
        Fingerprint = fingerprint;
    }

    public void SetThumbnail(string? thumbnailUrl)
    {
        ThumbnailUrl = thumbnailUrl;
    }

    public void Remove()
    {
        IsRemoved = true;
    }
}
