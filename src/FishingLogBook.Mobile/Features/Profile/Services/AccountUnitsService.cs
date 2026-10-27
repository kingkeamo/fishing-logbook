using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Clients;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Services;

public sealed class AccountUnitsService(
    IProfileClient profileClient,
    IAccountSessionCoordinator sessionCoordinator) : IAccountUnitsService
{
    private readonly Lock _gate = new();
    private Guid? _userId;
    private MeasurementUnitsModel _units = MeasurementUnitsModel.Default;

    public async Task<MeasurementUnitsModel> GetAsync(CancellationToken cancellationToken)
    {
        var lease = sessionCoordinator.Capture();
        if (lease is null)
        {
            return MeasurementUnitsModel.Default;
        }

        var known = TryGet(lease.UserId);
        if (known is not null)
        {
            return known;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lease.CancellationToken);
        var profile = await profileClient.GetAsync(linkedCancellation.Token);
        if (!profile.IsSuccess || !sessionCoordinator.IsCurrent(lease))
        {
            return MeasurementUnitsModel.Default;
        }

        Remember(lease, profile.Value!);
        return MeasurementUnitsModel.From(profile.Value!);
    }

    public void Remember(AccountSessionLeaseModel lease, ProfileDto profile)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(profile);
        if (!sessionCoordinator.IsCurrent(lease))
        {
            return;
        }

        lock (_gate)
        {
            _userId = lease.UserId;
            _units = MeasurementUnitsModel.From(profile);
        }
    }

    private MeasurementUnitsModel? TryGet(Guid userId)
    {
        lock (_gate)
        {
            return _userId == userId ? _units : null;
        }
    }
}
