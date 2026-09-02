using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchProvenanceEditorTests;

public class BaseCatchProvenanceEditorTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    protected static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid TripId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    protected static readonly DateTimeOffset StoredCaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z");

    protected static BunitContext CreateContext(
        ICatchClient? catchClient = null,
        ITripClient? tripClient = null,
        ITripParticipantClient? participantClient = null,
        INetworkService? network = null,
        ILoggingService? logging = null,
        ITimeService? time = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(catchClient ?? QuietCatchClient());
        context.Services.AddSingleton(tripClient ?? QuietTripClient());
        context.Services.AddSingleton(participantClient ?? QuietParticipantClient());
        context.Services.AddSingleton(network ?? OnlineNetwork());
        context.Services.AddSingleton(logging ?? QuietLogging());
        context.Services.AddSingleton(time ?? UtcTime());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static ICatchClient QuietCatchClient()
    {
        var client = Substitute.For<ICatchClient>();
        client.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CatchViewDto?)null);
        return client;
    }

    protected static ITripClient QuietTripClient()
    {
        var client = Substitute.For<ITripClient>();
        client.GetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TripDetailDto?)null);
        return client;
    }

    protected static ITripParticipantClient QuietParticipantClient()
    {
        var client = Substitute.For<ITripParticipantClient>();
        client.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TripParticipantsDto?)null);
        return client;
    }

    protected static INetworkService OnlineNetwork(bool isOnline = true)
    {
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(isOnline);
        return network;
    }

    protected static ILoggingService QuietLogging()
    {
        var logging = Substitute.For<ILoggingService>();
        logging.LogErrorAsync(Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return logging;
    }

    protected static ITimeService UtcTime()
    {
        return TestTimeService.WithOffset(TimeSpan.Zero);
    }

    protected static TripDetailDto TripDetail(string title = "Costello & Fermoyle")
    {
        return new TripDetailDto(new TripViewDto(TripId, OwnerUserId, "Active", StoredCaughtOn)
        {
            Title = title
        });
    }

    protected static TripParticipantsDto Participants(params TripParticipantDto[] participants)
    {
        return new TripParticipantsDto(TripId, TripParticipantConstants.Owner)
        {
            Participants = participants
        };
    }

    protected static TripParticipantDto Owner(Guid userId, string displayName)
    {
        return new TripParticipantDto(userId, TripParticipantConstants.Accepted, displayName, null, StoredCaughtOn)
        {
            IsOwner = true
        };
    }

    protected static TripParticipantDto Accepted(Guid userId, string displayName)
    {
        return new TripParticipantDto(userId, TripParticipantConstants.Accepted, displayName, null, StoredCaughtOn);
    }

    protected static CatchViewDto CatchView(Guid anglerUserId, Guid recordedByUserId, string? anglerName = null, string? recordedByName = null)
    {
        return new CatchViewDto(CatchId, anglerUserId, StoredCaughtOn)
        {
            CaughtByUserId = anglerUserId,
            AnglerName = anglerName,
            RecordedByUserId = recordedByUserId,
            RecordedByName = recordedByName,
            TripId = TripId
        };
    }
}
