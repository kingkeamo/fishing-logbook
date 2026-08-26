using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace FishingLogBook.Web.Features.Photographs.Services;

public interface IPhotographPreparationService
{
    Task<PhotographPreparationModel> PrepareAsync(
        IBrowserFile file,
        PhotographSourceEnum source,
        CancellationToken cancellationToken);
}
