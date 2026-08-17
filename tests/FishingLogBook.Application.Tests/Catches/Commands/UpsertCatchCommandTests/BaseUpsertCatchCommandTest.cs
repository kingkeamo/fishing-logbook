using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.UpsertCatchCommandTests;

public class BaseUpsertCatchCommandTest
{
    protected readonly ICatchService MockCatchService = Substitute.For<ICatchService>();

    protected readonly UpsertCatchHandler Sut;

    protected BaseUpsertCatchCommandTest()
    {
        Sut = new UpsertCatchHandler(MockCatchService);
    }
}
