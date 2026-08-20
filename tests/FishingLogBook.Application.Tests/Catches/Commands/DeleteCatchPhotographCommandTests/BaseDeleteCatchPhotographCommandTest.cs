using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.DeleteCatchPhotographCommandTests;

public class BaseDeleteCatchPhotographCommandTest
{
    protected readonly ICatchPhotographService MockCatchPhotographService =
        Substitute.For<ICatchPhotographService>();
    protected readonly DeleteCatchPhotographHandler Sut;

    protected BaseDeleteCatchPhotographCommandTest()
    {
        MockCatchPhotographService.IsObjectStorageConfigured.Returns(true);
        Sut = new DeleteCatchPhotographHandler(MockCatchPhotographService, TestMapper.Create());
    }
}
