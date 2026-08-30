namespace FishingLogBook.Application.Catches.Contracts.Services;

public interface ICatchPhotographObjectKeyBuilder
{
    string Build(Guid catchId, Guid photographId);
}
