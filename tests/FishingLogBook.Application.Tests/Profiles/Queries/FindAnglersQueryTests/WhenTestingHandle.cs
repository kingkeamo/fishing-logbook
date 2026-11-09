using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Application.Profiles.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Queries.FindAnglersQueryTests;

public class WhenTestingHandle
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IAnglerLookupService _mockService = Substitute.For<IAnglerLookupService>();
    private readonly FindAnglersHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new FindAnglersHandler(_mockService, TestMapper.Create());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.FindAsync(Arg.Any<FindAnglersArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<AnglerSummaryDto>>("The service failed."));

        // Act
        var response = await _sut.Handle(
            new FindAnglersQuery { RequestingUserId = UserId, Query = "sam", MaxResults = 5 },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Anglers.Should().BeEmpty();
        await _mockService.Received(1).FindAsync(
            Arg.Is<FindAnglersArgs>(args =>
                args.RequestingUserId == UserId && args.Query == "sam" && args.MaxResults == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapTheQueryAndReturnTheMatchingAnglers()
    {
        // Arrange
        IReadOnlyList<AnglerSummaryDto> anglers =
        [
            new AnglerSummaryDto(Guid.NewGuid(), "Sam", null, "Galway", "sam@example.com")
        ];
        _mockService.FindAsync(Arg.Any<FindAnglersArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(anglers));

        // Act
        var response = await _sut.Handle(
            new FindAnglersQuery { RequestingUserId = UserId, Query = "sam", MaxResults = 3 },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Anglers.Should().Equal(anglers);
        await _mockService.Received(1).FindAsync(
            Arg.Is<FindAnglersArgs>(args =>
                args.RequestingUserId == UserId && args.Query == "sam" && args.MaxResults == 3),
            Arg.Any<CancellationToken>());
    }
}
