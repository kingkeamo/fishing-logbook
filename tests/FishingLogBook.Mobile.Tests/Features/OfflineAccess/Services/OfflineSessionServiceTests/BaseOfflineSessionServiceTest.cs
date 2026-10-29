using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Services.OfflineSessionServiceTests;

public abstract class BaseOfflineSessionServiceTest
{
    protected static readonly OfflineCapabilityModel Capability = new(
        "subject-123",
        Guid.Parse("7f0a7d9e-82cd-43a8-a190-23425af60708"),
        true);

    protected readonly OfflineSessionService Sut = new();
}
