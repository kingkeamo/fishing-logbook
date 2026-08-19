using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Commands.RecordProfilePhotographCommandTests;

public class BaseRecordProfilePhotographCommandTest
{
    protected readonly IProfileService MockProfileService = Substitute.For<IProfileService>();
    protected readonly RecordProfilePhotographHandler Sut;

    protected BaseRecordProfilePhotographCommandTest()
    {
        Sut = new RecordProfilePhotographHandler(MockProfileService, TestMapper.Create());
    }

    protected static RecordProfilePhotographCommand Command(
        Guid userId,
        Guid photographId,
        string objectKey)
    {
        return new RecordProfilePhotographCommand
        {
            UserId = userId,
            Photograph = new RecordPhotographDto(photographId, objectKey, "image/jpeg")
        };
    }
}
