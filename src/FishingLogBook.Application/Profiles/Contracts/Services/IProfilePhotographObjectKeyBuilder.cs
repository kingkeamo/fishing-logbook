namespace FishingLogBook.Application.Profiles.Contracts.Services;

public interface IProfilePhotographObjectKeyBuilder
{
    string Build(Guid userId, Guid photographId);
}
