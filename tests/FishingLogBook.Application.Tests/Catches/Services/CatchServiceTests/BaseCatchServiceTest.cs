using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class BaseCatchServiceTest
{
    protected readonly ICatchRepository MockCatchRepository = Substitute.For<ICatchRepository>();

    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();

    protected readonly ICatchLocationPrivacyService MockCatchLocationPrivacyService =
        Substitute.For<ICatchLocationPrivacyService>();

    protected readonly CatchService Sut;

    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected BaseCatchServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        Sut = new CatchService(
            MockCatchRepository,
            MockCurrentUser,
            MockCatchLocationPrivacyService,
            TestMapper.Create());
    }
}
