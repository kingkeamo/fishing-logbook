using System.Security.Claims;

namespace FishingLogBook.Web.Features.Authentication.Services;

public interface ISignedInUserDisplayService
{
    string? GetEmail(ClaimsPrincipal? user);
}
