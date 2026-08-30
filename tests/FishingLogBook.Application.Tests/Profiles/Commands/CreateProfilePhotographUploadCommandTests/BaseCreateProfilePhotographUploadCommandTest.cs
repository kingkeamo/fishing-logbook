using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Commands.CreateProfilePhotographUploadCommandTests;

public class BaseCreateProfilePhotographUploadCommandTest
{
    protected readonly IProfileService MockProfileService = Substitute.For<IProfileService>();
    protected readonly CreateProfilePhotographUploadHandler Sut;

    protected BaseCreateProfilePhotographUploadCommandTest()
    {
        MockProfileService.IsObjectStorageConfigured.Returns(true);
        Sut = new CreateProfilePhotographUploadHandler(MockProfileService);
    }

    protected static CreateProfilePhotographUploadCommand Command(Guid userId, Guid photographId)
    {
        return new CreateProfilePhotographUploadCommand
        {
            UserId = userId,
            Request = new PhotographUploadRequestDto(photographId, "image/jpeg")
        };
    }
}
