namespace FishingLogBook.Application.Profiles.Contracts.Builders;

public interface IProfilePhotographObjectKeyBuilder
{
    string Build(Guid userId, Guid photographId);
}
