using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Users;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Api.Tests.UserEndpointsTests;

public class WhenTestingGetCurrent : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingGetCurrent(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenAuthorizationIsMissing()
    {
        // Arrange
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.UserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Any<FindUserIdentityArgs>(),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenTheSubjectIsMissing()
    {
        // Arrange
        using var factory = new SystemApiFactory();
        var token = TestJwt.CreateAccessToken(includeSubject: false);
        var client = factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/users/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await factory.UserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Any<FindUserIdentityArgs>(),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectTheRequestWhenTheEmailIsMissing()
    {
        // Arrange
        using var factory = new SystemApiFactory();
        var token = TestJwt.CreateAccessToken(includeEmail: false);
        var client = factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/users/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await factory.UserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Any<FindUserIdentityArgs>(),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFailWithoutUsingAFallbackUserIdWhenMappingFails()
    {
        // Arrange
        using var factory = new SystemApiFactory();
        factory.MappingFailed = true;
        var client = factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/users/current");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body.Should().NotContain(Guid.Empty.ToString());
        body.Should().NotContain("Failed to resolve FishingLogBook user.");
        body.Should().NotContain(TestJwt.Subject);
        await factory.UserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args => args.Subject == TestJwt.Subject),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAClientSuppliedUserId()
    {
        // Arrange
        var suppliedUserId = Guid.NewGuid();
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-UserId", suppliedUserId.ToString());

        // Act
        var response = await client.GetAsync($"/api/users/current?userId={suppliedUserId}");
        var body = await response.Content.ReadFromJsonAsync<CurrentUserDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.UserId.Should().NotBe(suppliedUserId);
        body.UserId.Should().NotBe(Guid.Empty);
        body.Email.Should().Be(TestJwt.Email);
        await _factory.UserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args =>
                args.Provider == IdentityProviderConstants.Cognito
                && args.Subject == TestJwt.Subject),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAClientSuppliedSubject()
    {
        // Arrange
        const string suppliedSubject = "attacker-subject";
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-Cognito-Sub", suppliedSubject);

        // Act
        var response = await client.GetAsync($"/api/users/current?sub={suppliedSubject}");
        var body = await response.Content.ReadFromJsonAsync<CurrentUserDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.UserId.Should().NotBe(Guid.Empty);
        body.Email.Should().Be(TestJwt.Email);
        body.Provider.Should().Be(IdentityProviderConstants.Cognito);
        body.Subject.Should().Be(TestJwt.Subject);
        await _factory.UserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args =>
                args.Provider == IdentityProviderConstants.Cognito
                && args.Subject == TestJwt.Subject),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args => args.Subject == suppliedSubject),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreAClientSuppliedEmail()
    {
        // Arrange
        const string trustedEmail = "trusted@example.test";
        const string suppliedEmail = "attacker@example.test";
        const string subject = "subject-ignore-email";
        var client = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: subject, email: trustedEmail));
        client.DefaultRequestHeaders.Add("X-Email", suppliedEmail);

        // Act
        var response = await client.GetAsync($"/api/users/current?email={suppliedEmail}");
        var body = await response.Content.ReadFromJsonAsync<CurrentUserDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.UserId.Should().NotBe(Guid.Empty);
        body.Email.Should().Be(trustedEmail);
        await _factory.UserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args =>
                args.Provider == IdentityProviderConstants.Cognito
                && args.Subject == subject),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.Received(1).CreateAsync(
            Arg.Is<User>(user => user.Email == trustedEmail && user.Id != Guid.Empty),
            Arg.Is<UserIdentity>(identity =>
                identity.Provider == IdentityProviderConstants.Cognito
                && identity.Subject == subject
                && identity.UserId != Guid.Empty),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args => args.Subject == suppliedEmail),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Is<User>(user => user.Email == suppliedEmail),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnDifferentUserIdsWhenTheCognitoSubjectsDiffer()
    {
        // Arrange
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var clientA = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "subject-a"));
        var clientB = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: "subject-b"));

        // Act
        var userA = await clientA.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var userB = await clientB.GetFromJsonAsync<CurrentUserDto>("/api/users/current");

        // Assert
        userA.Should().NotBeNull();
        userB.Should().NotBeNull();
        userA!.UserId.Should().NotBe(userB!.UserId);
        userA.UserId.Should().NotBe(Guid.Empty);
        userB.UserId.Should().NotBe(Guid.Empty);
        userA.Email.Should().Be(TestJwt.Email);
        userB.Email.Should().Be(TestJwt.Email);
        await _factory.UserIdentityRepository.Received(1).CreateAsync(
            Arg.Is<User>(user => user.Email == TestJwt.Email),
            Arg.Is<UserIdentity>(identity =>
                identity.Provider == IdentityProviderConstants.Cognito
                && identity.Subject == "subject-a"),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.Received(1).CreateAsync(
            Arg.Is<User>(user => user.Email == TestJwt.Email),
            Arg.Is<UserIdentity>(identity =>
                identity.Provider == IdentityProviderConstants.Cognito
                && identity.Subject == "subject-b"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnDifferentUserIdsWhenSubjectsShareAnEmail()
    {
        // Arrange
        const string email = "shared@example.test";
        var clientA = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "shared-email-a", email: email));
        var clientB = _factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(subject: "shared-email-b", email: email));

        // Act
        var userA = await clientA.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        var userB = await clientB.GetFromJsonAsync<CurrentUserDto>("/api/users/current");

        // Assert
        userA.Should().NotBeNull();
        userB.Should().NotBeNull();
        userA!.UserId.Should().NotBe(userB!.UserId);
        userA.UserId.Should().NotBe(Guid.Empty);
        userB.UserId.Should().NotBe(Guid.Empty);
        userA.Email.Should().Be(email);
        userB.Email.Should().Be(email);
        await _factory.UserIdentityRepository.Received(1).CreateAsync(
            Arg.Is<User>(user => user.Email == email),
            Arg.Is<UserIdentity>(identity =>
                identity.Provider == IdentityProviderConstants.Cognito
                && identity.Subject == "shared-email-a"),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.Received(1).CreateAsync(
            Arg.Is<User>(user => user.Email == email),
            Arg.Is<UserIdentity>(identity =>
                identity.Provider == IdentityProviderConstants.Cognito
                && identity.Subject == "shared-email-b"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReuseTheSameUserIdWhenTheSameIdentityMakesAnotherRequest()
    {
        // Arrange
        const string subject = "reuse-same-identity";
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));

        // Act
        var first = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var second = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/current");

        // Assert
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second!.UserId.Should().Be(first!.UserId);
        first.UserId.Should().NotBe(Guid.Empty);
        first.Email.Should().Be(TestJwt.Email);
        second.Email.Should().Be(TestJwt.Email);
        await _factory.UserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args =>
                args.Provider == IdentityProviderConstants.Cognito
                && args.Subject == subject),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.Received(1).UpdateEmailAsync(
            Arg.Is<User>(user =>
                user.Id == first.UserId &&
                user.Email == TestJwt.Email),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldResolveTheSameUserIdWhenRequestsRepresentSeparateDevices()
    {
        // Arrange
        const string subject = "separate-devices";
        var token = TestJwt.CreateAccessToken(subject: subject);
        var phoneA = _factory.CreateAuthenticatedClient(token);
        var phoneB = _factory.CreateAuthenticatedClient(token);

        // Act
        var fromPhoneA = await phoneA.GetFromJsonAsync<CurrentUserDto>("/api/users/current");
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var fromPhoneB = await phoneB.GetFromJsonAsync<CurrentUserDto>("/api/users/current");

        // Assert
        fromPhoneA.Should().NotBeNull();
        fromPhoneB.Should().NotBeNull();
        fromPhoneB!.UserId.Should().Be(fromPhoneA!.UserId);
        fromPhoneA.UserId.Should().NotBe(Guid.Empty);
        fromPhoneA.Email.Should().Be(TestJwt.Email);
        fromPhoneB.Email.Should().Be(TestJwt.Email);
        await _factory.UserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args =>
                args.Provider == IdentityProviderConstants.Cognito
                && args.Subject == subject),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.Received(1).UpdateEmailAsync(
            Arg.Is<User>(user =>
                user.Id == fromPhoneA.UserId &&
                user.Email == TestJwt.Email),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheResolvedUserIdWhenTheAccessTokenIsValid()
    {
        // Arrange
        const string subject = "first-valid-access-token";
        _factory.UserIdentityRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient(TestJwt.CreateAccessToken(subject: subject));

        // Act
        var response = await client.GetAsync("/api/users/current");
        var body = await response.Content.ReadFromJsonAsync<CurrentUserDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.UserId.Should().NotBe(Guid.Empty);
        body.Email.Should().Be(TestJwt.Email);
        await _factory.UserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args =>
                args.Provider == IdentityProviderConstants.Cognito
                && args.Subject == subject),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.Received(1).CreateAsync(
            Arg.Is<User>(user =>
                user.Email == TestJwt.Email &&
                user.Id != Guid.Empty),
            Arg.Is<UserIdentity>(identity =>
                identity.Provider == IdentityProviderConstants.Cognito
                && identity.Subject == subject
                && identity.UserId != Guid.Empty),
            Arg.Any<CancellationToken>());
        await _factory.UserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheCurrentUsersOfflineAccessPreference()
    {
        _factory.OfflineAccessPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/users/current/offline-access-preference");
        var body = await response.Content.ReadFromJsonAsync<OfflineAccessPreferenceDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be(new OfflineAccessPreferenceDto(false));
        await _factory.OfflineAccessPreferenceRepository.Received(1).GetAsync(
            Arg.Is<Guid>(value => value != Guid.Empty),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldEnableOfflineAccessUsingServerControlledIdentityAndTimestamp()
    {
        _factory.OfflineAccessPreferenceRepository.ClearReceivedCalls();
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PutAsJsonAsync(
            "/api/users/current/offline-access-preference",
            new OfflineAccessPreferenceDto(true, DateTimeOffset.MinValue));
        var body = await response.Content.ReadFromJsonAsync<OfflineAccessPreferenceDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Enabled.Should().BeTrue();
        body.EnabledAt.Should().Be(DateTimeOffset.Parse("2026-08-23T12:00:00Z"));
        await _factory.OfflineAccessPreferenceRepository.Received(1).SetAsync(
            Arg.Is<Guid>(value => value != Guid.Empty),
            true,
            Arg.Any<CancellationToken>());
    }
}
