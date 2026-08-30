using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Catches.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.RecordCatchPhotographCommandTests;

public class BaseRecordCatchPhotographCommandTest
{
    protected readonly ICatchPhotographService MockCatchPhotographService =
        Substitute.For<ICatchPhotographService>();
    protected readonly RecordCatchPhotographHandler Sut;

    protected BaseRecordCatchPhotographCommandTest()
    {
        Sut = new RecordCatchPhotographHandler(MockCatchPhotographService, TestMapper.Create());
    }
}
