using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Catches.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.UpdateCatchLocationVisibilityCommandTests;

public class BaseUpdateCatchLocationVisibilityCommandTest
{
    protected readonly ICatchService MockCatchService = Substitute.For<ICatchService>();

    protected readonly UpdateCatchLocationVisibilityHandler Sut;

    protected BaseUpdateCatchLocationVisibilityCommandTest()
    {
        Sut = new UpdateCatchLocationVisibilityHandler(MockCatchService, TestMapper.Create());
    }
}
