using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.TestCatches;
using FishingLogBook.Domain.TestCatches;
using NSubstitute;

namespace FishingLogBook.Application.Tests.TestCatches.TestCatchServiceTests;

public class BaseTestCatchServiceTest
{
    protected readonly ITestCatchRepository MockTestCatchRepository = Substitute.For<ITestCatchRepository>();
    protected readonly IObjectStorage MockObjectStorage = Substitute.For<IObjectStorage>();
    protected readonly TestCatchService Sut;

    protected BaseTestCatchServiceTest()
    {
        MockObjectStorage.IsConfigured.Returns(true);
        MockTestCatchRepository
            .UpsertAsync(Arg.Any<TestCatchRecord>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<TestCatchRecord>(0));
        Sut = new TestCatchService(MockTestCatchRepository, MockObjectStorage);
    }
}
