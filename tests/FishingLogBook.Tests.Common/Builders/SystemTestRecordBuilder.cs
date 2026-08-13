using FishingLogBook.Domain.SystemStatus;

namespace FishingLogBook.Tests.Common.Builders;

public sealed class SystemTestRecordBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "FishingLogBook database online";
    private DateTimeOffset _createdOn = DateTimeOffset.UtcNow;

    public SystemTestRecordBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public SystemTestRecordBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public SystemTestRecordBuilder WithCreatedOn(DateTimeOffset createdOn)
    {
        _createdOn = createdOn;
        return this;
    }

    public SystemTestRecord Build()
    {
        return new SystemTestRecord
        {
            Id = _id,
            Name = _name,
            CreatedOn = _createdOn
        };
    }
}
