using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Tests.Features.Photographs.Services.PhotographPreparationServiceTests;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportPhotoPreparationServiceTests;

public class BaseImportPhotoPreparationServiceTest : BasePhotographPreparationServiceTest
{
    protected static readonly byte[] OriginalBytes = [1, 2, 3];
    protected static readonly byte[] SanitisedBytes = [4, 5, 6];

    protected static TestContext CreateContext(PhotographHistoricalMetadataModel? historical = null)
    {
        var metadata = Substitute.For<IPhotographMetadataService>();
        metadata.ReadHistorical(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset>())
            .Returns(historical ?? MissingMetadata());
        metadata.Sanitise(Arg.Any<byte[]>(), Arg.Any<string>()).Returns(SanitisedBytes);

        var registry = Substitute.For<IImportPhotoBlobRegistryService>();
        registry.RegisterAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new ImportPhotoBlobRegistrationModel(
                $"token-{call.ArgAt<byte[]>(0)[0]}",
                $"blob:thumbnail-{call.ArgAt<byte[]>(0)[0]}"));
        registry.ClearAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        registry.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return new TestContext(new ImportPhotoPreparationService(metadata, registry, logging), metadata, registry, logging);
    }

    protected static PhotographHistoricalMetadataModel MissingMetadata()
    {
        return new PhotographHistoricalMetadataModel(null, null, 0, false, false, null, null);
    }

    protected sealed record TestContext(
        ImportPhotoPreparationService Sut,
        IPhotographMetadataService Metadata,
        IImportPhotoBlobRegistryService Registry,
        ILoggingService Logging);
}
