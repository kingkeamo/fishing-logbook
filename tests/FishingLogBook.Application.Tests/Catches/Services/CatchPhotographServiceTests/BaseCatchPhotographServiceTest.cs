using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Catches;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchPhotographServiceTests;

public class BaseCatchPhotographServiceTest
{
    protected static readonly Guid UserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid CatchId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid PhotographId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    protected readonly ICatchRepository MockCatchRepository =
        Substitute.For<ICatchRepository>();
    protected readonly IObjectStorage MockObjectStorage =
        Substitute.For<IObjectStorage>();
    protected readonly ICurrentUser MockCurrentUser =
        Substitute.For<ICurrentUser>();

    protected BaseCatchPhotographServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(UserId);
        MockObjectStorage.IsConfigured.Returns(true);
        MockObjectStorage.CreateUploadUrlAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/upload"));
        MockCatchRepository.GetPhotographAsync(
                Arg.Any<Application.Args.GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<CatchPhotograph?>(
                new CatchPhotograph
                {
                    Id = PhotographId,
                    CatchId = CatchId,
                    ContentType = "image/jpeg"
                }));
    }

    protected CatchPhotographService CreateSut()
    {
        return new CatchPhotographService(
            MockCatchRepository,
            MockObjectStorage,
            MockCurrentUser);
    }
}
