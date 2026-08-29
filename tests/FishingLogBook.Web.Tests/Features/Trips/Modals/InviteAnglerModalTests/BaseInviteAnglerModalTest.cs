using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Modals.InviteAngler;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.InviteAnglerModalTests;

public class BaseInviteAnglerModalTest
{
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid MatchedUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected static BunitContext CreateContext(
        IProfileClient profileClient,
        ITripParticipantClient? participantClient = null,
        ILoggingService? logging = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(profileClient);
        context.Services.AddSingleton(participantClient ?? Substitute.For<ITripParticipantClient>());
        context.Services.AddSingleton(logging ?? Substitute.For<ILoggingService>());
        context.Services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static async Task<IRenderedComponent<MudDialogProvider>> ShowModalAsync(BunitContext context)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<InviteAnglerModal>
        {
            { modal => modal.Model, new InviteAnglerModalModel(TripId) }
        };
        await dialogs.ShowAsync<InviteAnglerModal>(parameters, new DialogOptions());
        return cut;
    }

    protected static IProfileClient ClientFinding(params AnglerSummaryDto[] anglers)
    {
        var client = Substitute.For<IProfileClient>();
        client.FindAnglersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(anglers);
        return client;
    }

    protected static AnglerSummaryDto Angler(
        string? displayName = "John Connolly",
        string? homeRegion = null,
        Guid? userId = null)
    {
        return new AnglerSummaryDto(userId ?? MatchedUserId, displayName, null, homeRegion);
    }
}
