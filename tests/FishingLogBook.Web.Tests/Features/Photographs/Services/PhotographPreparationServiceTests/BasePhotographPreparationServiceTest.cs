using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Tests.Features.Photographs.Services.PhotographMetadataServiceTests;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Photographs.Services.PhotographPreparationServiceTests;

public class BasePhotographPreparationServiceTest : BasePhotographMetadataServiceTest
{
    protected static readonly DateTimeOffset FileModifiedOn =
        DateTimeOffset.Parse("2026-08-22T10:28:43+00:00");

    protected static PhotographPreparationService CreateSut(
        IPhotographMetadataService? metadata = null,
        ILoggingService? logging = null,
        ITimeService? time = null)
    {
        var timeService = time ?? TestTimeService.WithOffset(TimeSpan.Zero);
        return new PhotographPreparationService(
            metadata ?? new PhotographMetadataService(timeService),
            timeService,
            logging ?? QuietLogging());
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static IBrowserFile File(
        byte[] bytes,
        string contentType = PhotographContentTypeConstants.Jpeg,
        DateTimeOffset? lastModified = null,
        string name = "photo.jpg")
    {
        return new StubBrowserFile(bytes, contentType, lastModified ?? FileModifiedOn, name);
    }

    protected static IBrowserFile UnreadableFile(
        string contentType = PhotographContentTypeConstants.Jpeg)
    {
        return new UnreadableBrowserFile(contentType);
    }

    private sealed class StubBrowserFile : IBrowserFile
    {
        private readonly byte[] _bytes;

        public StubBrowserFile(
            byte[] bytes,
            string contentType,
            DateTimeOffset lastModified,
            string name)
        {
            _bytes = bytes;
            ContentType = contentType;
            LastModified = lastModified;
            Name = name;
        }

        public string Name { get; }

        public DateTimeOffset LastModified { get; }

        public long Size => _bytes.Length;

        public string ContentType { get; }

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (_bytes.Length > maxAllowedSize)
            {
                throw new IOException("Supplied file with size exceeds the maximum allowed size.");
            }

            return new MemoryStream(_bytes);
        }
    }

    private sealed class UnreadableBrowserFile : IBrowserFile
    {
        public UnreadableBrowserFile(string contentType)
        {
            ContentType = contentType;
        }

        public string Name => "unreadable.jpg";

        public DateTimeOffset LastModified => FileModifiedOn;

        public long Size => 1;

        public string ContentType { get; }

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            throw new IOException("The selected file at offset 42 in beach.jpg could not be opened.");
        }
    }
}
