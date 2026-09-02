using FishingLogBook.Application.Catches.Contracts.Builders;
using FishingLogBook.Application.Catches.Contracts.Repositories;
using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Domain.Catches;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
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
    protected readonly ICatchPhotographObjectKeyBuilder MockObjectKeyBuilder =
        Substitute.For<ICatchPhotographObjectKeyBuilder>();

    protected BaseCatchPhotographServiceTest()
    {
        MockObjectKeyBuilder.Build(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(call => $"catch-photographs/{call.ArgAt<Guid>(0):D}/{call.ArgAt<Guid>(1):D}");
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(UserId);
        MockObjectStorage.IsConfigured.Returns(true);
        MockObjectStorage.CreateUploadUrlAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new Uri("https://storage.test/upload"));
        MockCatchRepository.GetByIdAsync(CatchId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(new Catch
            {
                Id = CatchId,
                CaughtByUserId = UserId,
                RecordedByUserId = UserId
            }));
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
        MockCatchRepository.DeletePhotographAsync(
                Arg.Any<Application.Args.GetCatchPhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
    }

    protected CatchPhotographService CreateSut()
    {
        return new CatchPhotographService(
            MockCatchRepository,
            MockObjectStorage,
            MockCurrentUser,
            MockObjectKeyBuilder,
            NullLogger<CatchPhotographService>.Instance);
    }
}
