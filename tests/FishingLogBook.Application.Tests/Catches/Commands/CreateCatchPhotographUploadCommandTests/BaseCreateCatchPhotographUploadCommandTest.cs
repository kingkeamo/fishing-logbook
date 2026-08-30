using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Application.Common.Mappings;
using Mapster;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.CreateCatchPhotographUploadCommandTests;

public class BaseCreateCatchPhotographUploadCommandTest
{
    protected readonly ICatchPhotographService MockCatchPhotographService =
        Substitute.For<ICatchPhotographService>();
    protected readonly CreateCatchPhotographUploadHandler Sut;

    protected BaseCreateCatchPhotographUploadCommandTest()
    {
        ((IRegister)new CatchMappingRegistration()).Register(
            TypeAdapterConfig.GlobalSettings);
        MockCatchPhotographService.IsObjectStorageConfigured.Returns(true);
        Sut = new CreateCatchPhotographUploadHandler(MockCatchPhotographService, TestMapper.Create());
    }
}
