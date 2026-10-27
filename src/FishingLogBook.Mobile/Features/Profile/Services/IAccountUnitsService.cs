using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Profile.Services;

public interface IAccountUnitsService
{
    Task<MeasurementUnitsModel> GetAsync(CancellationToken cancellationToken);
    void Remember(AccountSessionLeaseModel lease, ProfileDto profile);
}
