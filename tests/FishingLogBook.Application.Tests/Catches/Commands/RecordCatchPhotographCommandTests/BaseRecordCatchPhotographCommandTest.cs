using FishingLogBook.Application.Catches.Commands;
using FishingLogBook.Application.Common.Mappings;
using FishingLogBook.Application.Contracts.Services;
using Mapster;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Commands.RecordCatchPhotographCommandTests;

public class BaseRecordCatchPhotographCommandTest
{
    protected readonly ICatchPhotographService MockCatchPhotographService =
        Substitute.For<ICatchPhotographService>();
    protected readonly RecordCatchPhotographHandler Sut;

    protected BaseRecordCatchPhotographCommandTest()
    {
        ((IRegister)new CatchMappingRegistration()).Register(
            TypeAdapterConfig.GlobalSettings);
        Sut = new RecordCatchPhotographHandler(MockCatchPhotographService);
    }
}
