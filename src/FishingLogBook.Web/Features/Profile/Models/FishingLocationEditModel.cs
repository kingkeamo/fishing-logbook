namespace FishingLogBook.Web.Features.Profile.Models;

public sealed class FishingLocationEditModel(Guid id, string name, bool isDefault)
{
    public Guid Id { get; } = id;

    public string Name { get; } = name;

    public bool IsDefault { get; set; } = isDefault;
}
