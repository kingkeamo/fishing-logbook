using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Users;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Users.Services.UserIdentityServiceTests;

public class WhenTestingResolve : BaseUserIdentityServiceTest
{
    private const string Email = "eamonn@example.test";

    [Fact]
    public async Task ItShouldFailWhenTheSubjectIsMissing()
    {
        // Arrange
        // Act
        var result = await Sut.ResolveAsync(ArgsFor("  "), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("External identity is missing.");
        await MockUserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Any<FindUserIdentityArgs>(),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWhenTheEmailIsMissing()
    {
        // Arrange
        const string subject = "cognito-subject-no-email";

        // Act
        var result = await Sut.ResolveAsync(ArgsFor(subject, "  "), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Authenticated email is missing.");
        await MockUserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Any<FindUserIdentityArgs>(),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheLookupFails()
    {
        // Arrange
        const string subject = "cognito-subject-db-failure";
        MockUserIdentityRepository
            .FindUserIdAsync(LookupFor(subject), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Guid?>("Failed to resolve FishingLogBook user."));

        // Act
        var result = await Sut.ResolveAsync(ArgsFor(subject), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to resolve FishingLogBook user.");
        await MockUserIdentityRepository.Received(1).FindUserIdAsync(
            LookupFor(subject),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenCreationFails()
    {
        // Arrange
        const string subject = "cognito-subject-create-failure";
        MockUserIdentityRepository
            .FindUserIdAsync(LookupFor(subject), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Guid?>(null));
        MockUserIdentityRepository
            .CreateAsync(Arg.Any<User>(), Arg.Any<UserIdentity>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Guid>("Failed to resolve FishingLogBook user."));

        // Act
        var result = await Sut.ResolveAsync(ArgsFor(subject), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to resolve FishingLogBook user.");
        await MockUserIdentityRepository.Received(1).CreateAsync(
            UserWithEmail(Email),
            IdentityFor(subject),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWithoutUsingAFallbackUserIdWhenCreateReturnsEmpty()
    {
        // Arrange
        const string subject = "cognito-subject-empty";
        MockUserIdentityRepository
            .FindUserIdAsync(LookupFor(subject), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Guid?>(null));
        MockUserIdentityRepository
            .CreateAsync(Arg.Any<User>(), Arg.Any<UserIdentity>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(Guid.Empty));

        // Act
        var result = await Sut.ResolveAsync(ArgsFor(subject), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("FishingLogBook UserId cannot be empty.");
        await MockUserIdentityRepository.Received(1).CreateAsync(
            UserWithEmail(Email),
            IdentityFor(subject),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWithoutUsingAFallbackUserIdWhenFindReturnsEmpty()
    {
        // Arrange
        const string subject = "cognito-subject-empty-find";
        MockUserIdentityRepository
            .FindUserIdAsync(LookupFor(subject), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Guid?>(Guid.Empty));

        // Act
        var result = await Sut.ResolveAsync(ArgsFor(subject), CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("FishingLogBook UserId cannot be empty.");
        await MockUserIdentityRepository.Received(1).FindUserIdAsync(
            LookupFor(subject),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCreateAUserWhenTheIdentityExists()
    {
        // Arrange
        const string subject = "cognito-subject-existing";
        var userId = Guid.NewGuid();
        MockUserIdentityRepository
            .FindUserIdAsync(LookupFor(subject), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Guid?>(userId));
        MockUserIdentityRepository
            .UpdateEmailAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var result = await Sut.ResolveAsync(ArgsFor(subject), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(userId);
        await MockUserIdentityRepository.Received(1).FindUserIdAsync(
            LookupFor(subject),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.Received(1).UpdateEmailAsync(
            Arg.Is<User>(user => user.Id == userId && user.Email == Email),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUpdateTheEmailWhenTheIdentityExists()
    {
        // Arrange
        const string subject = "cognito-subject-email-change";
        const string updatedEmail = "updated@example.test";
        var userId = Guid.NewGuid();
        MockUserIdentityRepository
            .FindUserIdAsync(LookupFor(subject), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Guid?>(userId));
        MockUserIdentityRepository
            .UpdateEmailAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var result = await Sut.ResolveAsync(ArgsFor(subject, updatedEmail), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(userId);
        await MockUserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.Received(1).UpdateEmailAsync(
            Arg.Is<User>(user => user.Id == userId && user.Email == updatedEmail),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLookupByProviderAndSubjectOnly()
    {
        // Arrange
        const string subject = "cognito-sub-abc";
        var userId = Guid.NewGuid();
        MockUserIdentityRepository
            .FindUserIdAsync(LookupFor(subject), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Guid?>(userId));
        MockUserIdentityRepository
            .UpdateEmailAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var result = await Sut.ResolveAsync(ArgsFor(subject), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(userId);
        await MockUserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args =>
                args.Provider == IdentityProviderConstants.Cognito
                && args.Subject == subject),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args => args.Subject == Email),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.Received(1).UpdateEmailAsync(
            Arg.Is<User>(user => user.Id == userId && user.Email == Email),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheSameUserIdWhenResolvedAgainForTheSameSubject()
    {
        // Arrange
        const string subject = "cognito-subject-stable";
        var userId = Guid.NewGuid();
        MockUserIdentityRepository
            .FindUserIdAsync(LookupFor(subject), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Guid?>(userId));
        MockUserIdentityRepository
            .UpdateEmailAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        // Act
        var first = await Sut.ResolveAsync(ArgsFor(subject), CancellationToken.None);
        var second = await Sut.ResolveAsync(ArgsFor(subject), CancellationToken.None);

        // Assert
        first.Value.Should().Be(userId);
        second.Value.Should().Be(userId);
        await MockUserIdentityRepository.Received(2).FindUserIdAsync(
            LookupFor(subject),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.Received(2).UpdateEmailAsync(
            Arg.Is<User>(user => user.Id == userId && user.Email == Email),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnDifferentUserIdsWhenSubjectsShareAnEmail()
    {
        // Arrange
        const string subjectA = "cognito-subject-a";
        const string subjectB = "cognito-subject-b";
        const string sharedEmail = "shared@example.test";
        MockUserIdentityRepository
            .FindUserIdAsync(Arg.Any<FindUserIdentityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Guid?>(null));
        MockUserIdentityRepository
            .CreateAsync(Arg.Any<User>(), Arg.Any<UserIdentity>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<User>(0).Id));

        // Act
        var resultA = await Sut.ResolveAsync(ArgsFor(subjectA, sharedEmail), CancellationToken.None);
        var resultB = await Sut.ResolveAsync(ArgsFor(subjectB, sharedEmail), CancellationToken.None);

        // Assert
        resultA.Value.Should().NotBe(resultB.Value);
        await MockUserIdentityRepository.Received(1).FindUserIdAsync(
            LookupFor(subjectA),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.Received(1).FindUserIdAsync(
            LookupFor(subjectB),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.Received(1).CreateAsync(
            UserWithEmail(sharedEmail),
            IdentityFor(subjectA),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.Received(1).CreateAsync(
            UserWithEmail(sharedEmail),
            IdentityFor(subjectB),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCreateAUserWhenNoMappingExists()
    {
        // Arrange
        const string subject = "cognito-subject-new";
        User? persistedUser = null;
        UserIdentity? persistedIdentity = null;
        MockUserIdentityRepository
            .FindUserIdAsync(LookupFor(subject), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Guid?>(null));
        MockUserIdentityRepository
            .CreateAsync(Arg.Any<User>(), Arg.Any<UserIdentity>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                persistedUser = call.ArgAt<User>(0);
                persistedIdentity = call.ArgAt<UserIdentity>(1);
                return Result.Ok(persistedUser.Id);
            });

        // Act
        var result = await Sut.ResolveAsync(ArgsFor(subject), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        persistedUser.Should().NotBeNull();
        persistedIdentity.Should().NotBeNull();
        persistedUser!.Email.Should().Be(Email);
        persistedIdentity!.Provider.Should().Be(IdentityProviderConstants.Cognito);
        persistedIdentity.Subject.Should().Be(subject);
        persistedIdentity.UserId.Should().Be(persistedUser.Id);
        result.Value.Should().Be(persistedUser.Id);
        await MockUserIdentityRepository.Received(1).FindUserIdAsync(
            LookupFor(subject),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.Received(1).CreateAsync(
            Arg.Is<User>(user =>
                user.Id == persistedUser.Id &&
                user.Email == Email),
            Arg.Is<UserIdentity>(identity =>
                identity.Id != Guid.Empty &&
                identity.UserId == persistedUser.Id &&
                identity.Provider == IdentityProviderConstants.Cognito &&
                identity.Subject == subject),
            Arg.Any<CancellationToken>());
        await MockUserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }
}
