using FishingLogBook.Domain.SystemStatus;

namespace FishingLogBook.Tests.Common.Builders;

public sealed class SystemHealthBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "FishingLogBook database online";
    private DateTimeOffset _createdOn = DateTimeOffset.UtcNow;

    public SystemHealthBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public SystemHealthBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public SystemHealthBuilder WithCreatedOn(DateTimeOffset createdOn)
    {
        _createdOn = createdOn;
        return this;
    }

    public SystemHealth Build()
    {
        return new SystemHealth
        {
            Id = _id,
            Name = _name,
            CreatedOn = _createdOn
        };
    }
}
