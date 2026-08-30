namespace FishingLogBook.Application.Catches.Contracts.Builders;

public interface ICatchPhotographObjectKeyBuilder
{
    string Build(Guid catchId, Guid photographId);
}
