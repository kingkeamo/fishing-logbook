using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripStoreTests;

public class WhenTestingSharedTripAccess : IDisposable
{
    private const string Module = "./js/offline-store.js";

    private static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ParticipantUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-17T09:00:00Z");

    private readonly BunitContext _context = new();
    private readonly BunitJSModuleInterop _module;
    private readonly IndexedDbTripStore _sut;

    public WhenTestingSharedTripAccess()
    {
        _module = _context.JSInterop.SetupModule(Module);
        _sut = new IndexedDbTripStore(
            _context.Services.GetRequiredService<IJSRuntime>(),
            Substitute.For<IDiagnosticLogger>(),
            Substitute.For<ILoggingService>(),
            new DiagnosticsClientConfig { OperationTimeoutMilliseconds = 5000 });
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static TripModel SharedTrip()
    {
        return new TripModel(TripId, OwnerUserId, TripConstants.Active, StartedOn)
        {
            ParticipantUserIds = [ParticipantUserId]
        };
    }

    [Fact]
    public async Task ItShouldRefuseToCacheASharedTripForSomeoneWhoCannotContribute()
    {
        // Arrange
        // Act
        var act = () => _sut.HydrateAsync(SharedTrip(), StrangerUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _module.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRefuseToCacheATripWithNoOwner()
    {
        // Arrange
        var trip = SharedTrip() with { OwnerUserId = Guid.Empty };

        // Act
        var act = () => _sut.HydrateAsync(trip, ParticipantUserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _module.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldCacheASharedTripForTheOwnerOrAParticipant(bool asOwner)
    {
        // Arrange
        _module.Setup<string>("hydrateTrip", _ => true).SetResult("saved");
        var viewer = asOwner ? OwnerUserId : ParticipantUserId;

        // Act
        await _sut.HydrateAsync(SharedTrip(), viewer, CancellationToken.None);

        // Assert
        var call = _module.Invocations["hydrateTrip"].Should().ContainSingle().Subject;
        call.Arguments[0].Should().BeOfType<string>().Which.Should().Contain(TripId.ToString("D"));
        call.Arguments[1].Should().Be(viewer.ToString("D"));
    }

    [Fact]
    public async Task ItShouldRevokeAParticipantsAccessToACachedTrip()
    {
        // Arrange
        _module.Setup<bool>("revokeParticipantAccess", _ => true).SetResult(true);

        // Act
        var revoked = await _sut.RevokeParticipantAccessAsync(ParticipantUserId, TripId, CancellationToken.None);

        // Assert
        revoked.Should().BeTrue();
        _module.Invocations["revokeParticipantAccess"].Should().ContainSingle()
            .Which.Arguments.Should().Equal(ParticipantUserId.ToString("D"), TripId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldReportNothingRevokedWhenThereIsNothingCached()
    {
        // Arrange
        _module.Setup<bool>("revokeParticipantAccess", _ => true).SetResult(false);

        // Act
        var revoked = await _sut.RevokeParticipantAccessAsync(ParticipantUserId, TripId, CancellationToken.None);

        // Assert
        revoked.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRefuseToRevokeWithoutAViewer()
    {
        // Arrange
        // Act
        var act = () => _sut.RevokeParticipantAccessAsync(Guid.Empty, TripId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _module.Invocations.Should().BeEmpty();
    }
}
