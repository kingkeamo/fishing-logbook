using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripPhotographsTests;

public class BaseTripPhotographsTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T07:00:00Z");

    protected static BunitContext CreateContext(
        ITripPhotographStore store,
        IPhotographPreparationService? preparation = null,
        ITripClient? tripClient = null,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(store);
        context.Services.AddSingleton(preparation ?? PreparationFor(Metadata()));
        context.Services.AddSingleton(tripClient ?? Substitute.For<ITripClient>());
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static IPhotographPreparationService PreparationFor(
        PhotographMetadataModel metadata,
        PhotographSourceEnum source = PhotographSourceEnum.Gallery)
    {
        var preparation = Substitute.For<IPhotographPreparationService>();
        preparation.PrepareAsync(
                Arg.Any<IBrowserFile>(),
                Arg.Any<PhotographSourceEnum>(),
                Arg.Any<CancellationToken>())
            .Returns(call => PhotographPreparationModel.Prepared(
                new PreparedPhotographModel(
                    Guid.NewGuid(),
                    PhotographContentTypeConstants.Jpeg,
                    [9, 9, 9],
                    call.ArgAt<PhotographSourceEnum>(1),
                    metadata,
                    CapturedOnLocal: null)));
        return preparation;
    }

    protected static IPhotographPreparationService FailingPreparation()
    {
        var preparation = Substitute.For<IPhotographPreparationService>();
        preparation.PrepareAsync(
                Arg.Any<IBrowserFile>(),
                Arg.Any<PhotographSourceEnum>(),
                Arg.Any<CancellationToken>())
            .Returns(PhotographPreparationModel.CouldNotPrepare);
        return preparation;
    }

    protected static PhotographMetadataModel Metadata(
        DateTimeOffset? capturedOn = null,
        PhotographCapturedOnSourceEnum source = PhotographCapturedOnSourceEnum.None,
        double? latitude = null,
        double? longitude = null)
    {
        return new PhotographMetadataModel(capturedOn, latitude, longitude, source);
    }

    protected static PhotographMetadataModel StrippedMetadata()
    {
        return PhotographMetadataModel.Empty;
    }

    protected static TripModel Trip(params TripPhotographModel[] photographs)
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            TripConstants.Active,
            StartedOn,
            Photographs: photographs.Length == 0 ? null : photographs);
    }

    protected static TripPhotographModel StoredPhotograph(
        Guid photographId,
        DateTimeOffset? capturedOn = null,
        Web.Common.SyncStatus syncStatus = Web.Common.SyncStatus.SavedLocally)
    {
        return new TripPhotographModel(
            photographId,
            TripId,
            OwnerUserId,
            PhotographContentTypeConstants.Jpeg,
            StartedOn.AddHours(1),
            capturedOn,
            SyncStatus: syncStatus);
    }

    protected static InputFileContent JpegFile(string name)
    {
        return InputFileContent.CreateFromBinary(
            [0xFF, 0xD8, 0xFF],
            name,
            contentType: PhotographContentTypeConstants.Jpeg);
    }
}
