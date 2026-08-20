---
paths:
  - "tests/FishingLogBook.Tests.Common/**/*.cs"
  - "tests/FishingLogBook.Shared.Tests/**/*.cs"
  - "tests/FishingLogBook.Application.Tests/**/*.cs"
  - "tests/FishingLogBook.Infrastructure.Tests/**/*.cs"
  - "tests/FishingLogBook.Db.Migrations.Tests/**/*.cs"
  - "tests/FishingLogBook.Api.Tests/**/*.cs"
---

# C# Testing Conventions (Unit & Integration)

Blazor / bUnit tests are in **`testing-blazor.md`**.

## Issue test requirements (mandatory)

Tests are part of every feature and are not optional follow-up work.

For every GitHub issue:

1. Review every Acceptance Criterion.
2. Add automated tests for each criterion where technically practical.
3. Prefer behavioural tests over implementation-detail tests.
4. Follow this file and **`testing-blazor.md`**.
5. Use the testing libraries already selected by the solution.
6. Do not introduce a new testing framework without a specific reason.
7. Do not add meaningless tests purely to increase coverage.
8. Test failure paths where Acceptance Criteria describe failure behaviour.
9. Offline features must test offline persistence and reconnection behaviour.
10. Location features must test permission granted, denied and unavailable scenarios where relevant.
11. Synchronisation tests must verify retry behaviour does not create duplicate server records.
12. Existing tests must remain green.
13. Run the appropriate test projects before considering implementation complete.

## Test projects (one per production project)

Each production project has its **own** `FishingLogBook.<Project>.Tests` project; shared
builders/fixtures live in `FishingLogBook.Tests.Common`. Do **not** add tests to a
different project's test project.

| Project | Scope | References |
|---------|-------|------------|
| `FishingLogBook.Tests.Common` | Shared test builders/fixtures — **no tests** (plain class library) | Domain, Shared |
| `FishingLogBook.Shared.Tests` | DTO / contract serialisation | Shared, Tests.Common |
| `FishingLogBook.Application.Tests` | CQRS handlers + FluentValidation validators | Application, Tests.Common |
| `FishingLogBook.Infrastructure.Tests` | Unit tests at normal SUT/feature paths; live-infrastructure tests under `Repositories/` (Testcontainers in CI) | Infrastructure, Tests.Common |
| `FishingLogBook.Db.Migrations.Tests` | Migration ordering (`FilenameOnlyScriptComparer`) and engine helpers | Db.Migrations, Tests.Common |
| `FishingLogBook.Api.Tests` | API endpoints via `WebApplicationFactory<Program>` (repositories mocked — no live DB in CI) | Api, Shared, Application, Tests.Common |

`Domain`, `DependencyInjection`, and `Db.Migrations.App` have no dedicated test project yet
(POCOs / wiring / console host). Add one as `FishingLogBook.<Project>.Tests` when they gain
testable logic.

## Production code — ask before changing (mandatory)

**Never modify production code under `src/` without explicit user approval first.** If a
test fails:

1. **Fix the test** (assertions, test data, mocks) — default action.
2. If the failure reveals a genuine production issue, **propose** the change and **stop** —
   wait for approval before editing `src/`.

Allowed without asking: files under `tests/`, `.editorconfig`, and rule docs when the task
is test/tooling work only.

## Purpose: tests must catch code changes

A unit test must **fail when production behaviour changes**. Tests that pass regardless of
what the code does (asserting only `IsSuccess`, or `Received(Arg.Any<T>())` when the code
passes specific values) are not protecting the codebase.

### Sufficient assertions (every test)

Assert more than "no exception":

| Layer | Minimum assertions |
|-------|-------------------|
| CQRS handlers | Outcome (`IsSuccess` / `IsFailure`, `ErrorMessage`, returned data) **and** `Received(n)` / `DidNotReceive()` with `Arg.Is<>` on meaningful inputs |
| Application services | FluentResults outcome **and** `Received(n)` / `DidNotReceive()` with `Arg.Is<>` on meaningful inputs |
| Validators | `ShouldHaveValidationErrorFor` / `ShouldNotHaveAnyValidationErrors` for the rule under test |
| API endpoints (integration) | HTTP status **and** response body **and**, when repositories are substituted, meaningful `Received(n)` / `DidNotReceive()` on those dependencies |
| Infrastructure unit tests | Observable result **and** `Received(n)` / `DidNotReceive()` when collaborators are substituted |
| Web / bUnit | See **`testing-blazor.md`** |

**Anti-patterns (insufficient):** `result.IsSuccess.Should().BeTrue()` only; `Received(Arg.Any<T>())` when the SUT passes specific values; `Received()` without a call count; a success test with no `Received(n)` verification; inferring a dependency was not called only because the return value looks correct.

After writing a test, mentally invert one line of production behaviour it covers — the test
must fail. If it would not, add assertions.

## Dependency verification (mandatory)

Whenever the System Under Test calls a mocked/substituted dependency, the test must
normally verify that interaction.

A good test proves **both**:

1. Observable result/behaviour.
2. The meaningful dependency interactions that produced that behaviour.

This applies to CQRS handler tests, application service tests, API tests where
dependencies are substituted, Infrastructure unit tests, Web/bUnit component tests,
Web service tests, and any other test using NSubstitute collaborators.

Do **not** add interaction assertions to tests that have no mocked dependencies, or to
true integration/state tests whose behaviour is the persisted or external result
(Testcontainers, real HTTP, in-memory store used as the system under test).

### Meaningful input assertions

Use `Received(n)`, `DidNotReceive(...)`, and `Arg.Is<T>(...)` to prove meaningful
values cross architectural boundaries correctly.

`Received()` **without a count is forbidden**. It only proves the dependency was
called at least once, so extra unexpected calls still pass. Always pass the expected
number: `Received(1)`, `Received(2)`, …

`DidNotReceive()` is the correct assertion when the dependency must not be called.

```csharp
// BAD — extra GetAllAsync calls would still pass
await store.Received().GetAllAsync(Arg.Any<CancellationToken>());

// GOOD
await store.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
```

`Arg.Any<T>()` is acceptable only where the value genuinely does not matter to the
behaviour under test.

Typically acceptable:

- `Arg.Any<CancellationToken>()`
- `Arg.Any<T>()` on `DidNotReceive()` when proving the dependency was **not invoked at
  all** (any argument would be wrong)

Typically **not** acceptable on `Received()` when the SUT is expected to
construct/pass the value:

- `Arg.Any<User>()`, `Arg.Any<UserIdentity>()`, `Arg.Any<FindUserIdentityArgs>()`, `Arg.Any<Catch>()`
- `Arg.Any<Guid>()` for UserId / record identity
- `Arg.Any<string>()` for Provider / Subject / Email
- `Arg.Any<Dto>()`, `Arg.Any<Model>()`, `Arg.Any<Command>()`, `Arg.Any<Request>()`

Instead assert the relevant properties.

```csharp
// BAD
await MockRepository.Received(1)
    .CreateAsync(
        Arg.Any<User>(),
        Arg.Any<UserIdentity>(),
        Arg.Any<CancellationToken>());

// GOOD
await MockRepository.Received(1)
    .CreateAsync(
        Arg.Is<User>(user =>
            user.Id == expectedUserId &&
            user.Email == expectedEmail),
        Arg.Is<UserIdentity>(identity =>
            identity.UserId == expectedUserId &&
            identity.Provider == expectedProvider &&
            identity.Subject == expectedSubject),
        Arg.Any<CancellationToken>());
```

`Returns(Arg.Any<T>())` in Arrange is a stub. The **Assert** `Received()` still needs
`Arg.Is<>` for values the SUT must pass.

### Negative interaction assertions

Where behaviour requires a dependency **not** to be invoked, prove it with
`DidNotReceive()`. Do not infer "not called" from the return value alone.

Examples:

- validation failure → service / repository not called
- lookup finds an existing identity → create not called
- initial persistence failure → later persistence operation not called
- unauthenticated / unauthorized request → protected repository not called
- missing catch → photograph upload URL not created

### Do not over-assert implementation details

This rule does **not** mean asserting every internal method call. Only verify
interactions across a **substituted dependency boundary** that are part of the
behavioural contract. Tests should remain resilient to internal refactoring.

GOOD: the service passes the correct `User` domain object to `IUserIdentityRepository`.

NOT REQUIRED: asserting calls between private helpers inside the same service.

## Stack

Use only this stack:

- xUnit
- NSubstitute
- **AwesomeAssertions** (`using AwesomeAssertions;`)
- FluentValidation.TestHelper
- coverlet.collector
- `Microsoft.AspNetCore.Mvc.Testing` for API integration tests

Do **not** add the FluentAssertions package. Do not write `using FluentAssertions;`.
AwesomeAssertions is the assertion library.

Handler tests belong in `FishingLogBook.Application.Tests`. They instantiate the
handler directly with `Substitute.For<I*Service>()` — they do not run the MediatR
pipeline unless the test is specifically covering pipeline integration.

- `Fact` is available via the project's global `Using Include="Xunit"`.
- Never hand-roll fakes/stubs — use NSubstitute (`Substitute.For<T>()`, `Returns`,
  `Arg.Any<T>()`, `Arg.Is<T>(...)`, `Received(n)`, `DidNotReceive()`).
  `Received()` without a count is forbidden.
- Use `Arg.Is<>` (or the exact value) rather than `Arg.Any<>` when the actual passed
  value matters. The only hand-rolled doubles that remain acceptable are ones
  NSubstitute cannot replace cleanly: in-memory persistence used **as the system
  under test's backing store**, bUnit `NavigationManager` subclasses, `IJSRuntime`
  timeout/interop doubles, and `ILogger` spies that capture scopes. Collaborators
  of a higher SUT must still be NSubstitute.
- NSubstitute fields: `Mock*` prefix (`MockSystemRepository`) — same as rah-portal.

## Naming & structure — `WhenTesting` convention (mandatory)

Follow the rah-portal / RefAssured `WhenTesting` layout. Mirror production namespaces.

The structure is organised around the **public method** being tested, not around
each scenario.

```text
{Sut}Tests/
    Base{Sut}Test.cs
    WhenTesting{Method}.cs
```

```text
tests/FishingLogBook.Application.Tests/{Feature}/Commands/{Name}CommandTests/WhenTestingHandle.cs
tests/FishingLogBook.Application.Tests/{Feature}/Commands/{Name}CommandValidatorTests/WhenTestingValidate.cs
tests/FishingLogBook.Application.Tests/{Feature}/Queries/{Name}QueryTests/WhenTestingHandle.cs
tests/FishingLogBook.Application.Tests/{Feature}/Services/{Name}ServiceTests/WhenTestingResolve.cs
tests/FishingLogBook.Api.Tests/UserEndpointsTests/WhenTestingGetCurrent.cs
tests/FishingLogBook.Infrastructure.Tests/Repositories/Repositories/UserIdentityRepositoryTests/WhenTestingCreate.cs
```

### One folder per SUT

`{Sut}Tests/` (for example `UserIdentityServiceTests/`, `ResolveCurrentUserCommandTests/`).

### Base class — no test methods

`Base{Sut}Test` in that folder holds SUT construction, shared substitutes,
shared constants, and common setup helpers as `protected` fields, constructed in
the constructor (no `[SetUp]`). It must **not** contain `[Fact]` / `[Theory]`
methods. Do not over-abstract Arrange logic; scenario-specific setup stays in
the individual test.

```csharp
public class BaseUserIdentityServiceTest
{
    protected readonly IUserIdentityRepository MockUserIdentityRepository =
        Substitute.For<IUserIdentityRepository>();
    protected readonly UserIdentityService Sut;

    protected BaseUserIdentityServiceTest()
    {
        Sut = new UserIdentityService(
            MockUserIdentityRepository,
            NullLogger<UserIdentityService>.Instance);
    }
}
```

Handlers are constructed with `I*Service`, **never** `I*Repository`:

```csharp
public class BaseResolveCurrentUserCommandTest
{
    protected readonly IUserIdentityService MockUserIdentityService =
        Substitute.For<IUserIdentityService>();
    protected readonly ResolveCurrentUserHandler Sut;

    protected BaseResolveCurrentUserCommandTest()
    {
        Sut = new ResolveCurrentUserHandler(MockUserIdentityService);
    }
}
```

### One class per public method

`WhenTesting{Method}` inherits the base. The class name is the SUT public method
**without** `Async`.

| SUT method | Test class |
|------------|------------|
| `UserIdentityService.ResolveAsync(...)` | `WhenTestingResolve` |
| `CurrentUser.Assign(...)` | `WhenTestingAssign` |
| `ResolveCurrentUserHandler.Handle(...)` | `WhenTestingHandle` |
| `ResolveCurrentUserCommandValidator.Validate(...)` | `WhenTestingValidate` |
| `CatchService.UpsertAsync(...)` | `WhenTestingUpsert` |
| `GET /api/users/current` | `WhenTestingGetCurrent` |

`WhenTestingResolve` contains **all** meaningful scenarios for `ResolveAsync`.
Do **not** create one class/file per scenario.

BAD:

- `WhenTestingMissingSubject`
- `WhenTestingExistingIdentity`
- `WhenTestingRepositoryFailure`
- `WhenTestingHandleWhenTheServiceFails`

GOOD:

- `WhenTestingResolve` with `ItShouldFailWhenTheSubjectIsMissing`, …
- `WhenTestingHandle` with `ItShouldReturnFailureWhenTheServiceFails`, …

### Test method names

Methods describe the behaviour/scenario:

`ItShould{ExpectedBehaviour}()`

or, where the scenario must be in the name:

`ItShould{ExpectedBehaviour}When{Scenario}()`

Examples:

- `ItShouldFailWhenTheSubjectIsMissing`
- `ItShouldReturnFailureWhenTheRepositoryFails`
- `ItShouldNotCreateAUserWhenTheIdentityExists`
- `ItShouldCreateAUserWhenNoMappingExists`

Do **not** use underscores.

BAD: `ItShouldFail_WhenSubjectIsMissing`
BAD: `ItShouldClearLocation_WhenUpsertedWithoutLocation`
GOOD: `ItShouldFailWhenTheSubjectIsMissing`
GOOD: `ItShouldClearLocationWhenUpsertedWithoutLocation`

### Order inside a WhenTesting class

Default order, unless a different order is clearer:

1. Guard / invalid-input / validation
2. Failure / dependency-error
3. Negative / no-op / existing-state
4. Alternative successful scenarios
5. Principal happy path **last**

The file should read from defensive behaviour down to success.

Example for `WhenTestingResolve`:

1. `ItShouldFailWhenTheSubjectIsMissing`
2. `ItShouldFailWhenTheEmailIsMissing`
3. `ItShouldReturnFailureWhenTheLookupFails`
4. `ItShouldReturnFailureWhenCreationFails`
5. `ItShouldNotCreateAUserWhenTheIdentityExists`
6. `ItShouldUpdateTheEmailWhenTheIdentityExists`
7. `ItShouldCreateAUserWhenNoMappingExists`

### CQRS tests

```text
ResolveCurrentUserCommandTests/
    BaseResolveCurrentUserCommandTest.cs
    WhenTestingHandle.cs

ResolveCurrentUserCommandValidatorTests/
    BaseResolveCurrentUserCommandValidatorTest.cs
    WhenTestingValidate.cs
```

`WhenTestingHandle` contains service-failure, empty UserId, and success.
`WhenTestingValidate` contains **all** validation scenarios. Do not create one
validator class per invalid property.

Mirror the production namespace/type under the test project via these folders.

## Arrange / Act / Assert

Every test method uses exactly these section comments — no other comments:

```csharp
// Arrange
// Act
// Assert
```

## Handler tests (Application.Tests)

- Instantiate the handler directly with `Substitute.For<I*Service>()` — do not run the
  full MediatR pipeline unless testing integration.
- Call `await Sut.Handle(command, CancellationToken.None)`.
- Assert `result.IsFailure` / `IsSuccess`, `ErrorMessage`, `ValidationErrors`, and success
  data.
- **Every test** must verify mocks with `Received(n)` / `DidNotReceive()` and `Arg.Is<T>(...)`
  for meaningful inputs (see **Dependency verification**). When the handler maps its input,
  `Arg.Is<TArgs>` on the mapped fields is what proves the mapping copied the command — do
  not replace that with `Arg.Any<TArgs>()`.
- Handlers that adapt their input take `IMapper` in the constructor. Build an **isolated**
  mapper per test — never shared static configuration. See **Mapster in tests** below.

```csharp
Sut = new UpdateOwnProfileHandler(MockProfileService, TestMapper.Create());
```

- Build domain inputs with the shared builders from `FishingLogBook.Tests.Common`.
- API tests still mock **repositories** (not `IMediator`) so the real handler and service
  run inside `WebApplicationFactory`.

## Service tests (Application.Tests)

- Extend `Base{Service}Test`. Construct the service with `Substitute.For<I*Repository>()`.
- A service that maps takes `IMapper` in its constructor. Give it an isolated mapper —
  never shared static configuration, and never a single `IRegister` registered by hand,
  which leaves a partial config and makes the suite order dependent. See
  **Mapster in tests**.

```csharp
Sut = new CatchService(
    MockCatchRepository,
    MockCurrentUser,
    MockCatchLocationPrivacyService,
    TestMapper.Create());
```

- Assert FluentResults `IsSuccess` / `IsFailed` **and** `Received(n)` / `DidNotReceive()`
  with `Arg.Is<>` on meaningful inputs (see **Dependency verification**).

## Mapster in tests (mandatory)

Production owns its Mapster configuration through DI and must never touch
`TypeAdapterConfig.GlobalSettings` (**`cqrs.md` → Mapster**, solution-wide — Application and
Infrastructure). Tests follow the same rule in every test project whose SUT takes `IMapper`,
including `FishingLogBook.Infrastructure.Tests`.

`TypeAdapterConfig.GlobalSettings` is **process-wide mutable state**, and xUnit runs test
classes in parallel. Sharing it corrupts mappings two ways:

1. **Race.** `Scan` calls `NewConfig`, which resets a rule before re-adding its `.Map`
   calls. Concurrent registration on the same static config intermittently yields mappings
   with dropped properties. The tell is that properties mapped from a *nested*
   path come back null while same-named top-level properties survive — so it reads like a product
   bug and gets "fixed" in the wrong place. It fails perhaps one run in four, and passes in
   isolation.
2. **Order dependence.** A class that registers only its own `IRegister` leaves a *partial*
   config behind. Whether another class sees the mapping it needs depends on which test ran
   first, and on a configuration that never matches production.

Build a **fresh, isolated** config and mapper instead, scanning the same assembly the
composition root scans so tests see the configuration production actually builds:

```csharp
public static class TestMapper
{
    public static IMapper Create()
    {
        var typeAdapterConfig = new TypeAdapterConfig();
        typeAdapterConfig.Scan(typeof(CatchMappingRegistration).Assembly);
        return new Mapper(typeAdapterConfig);
    }
}
```

Put it in `{TestProject}/Common/TestMapper.cs`, expose the namespace as a global `<Using>`
in the csproj, and pass `TestMapper.Create()` where the SUT takes an `IMapper`. Nothing is
shared, so no lock and no initialisation flag are needed.

A suite whose SUT takes no `IMapper` needs no mapper at all — do not wire one in
"just in case".

**Do not** disable test parallelisation, put otherwise-independent tests into one
collection, or share a single `WebApplicationFactory`, to hide a Mapster race. Those mask
the problem, slow the suite, and leave the order dependence in place.
`DisableTestParallelization` is only for projects with genuinely process-wide test state
that cannot be removed — for example a suite that mutates `CultureInfo.CurrentCulture`.

**Do not** add a static lock or a `_registered` flag to production so that repeated
container composition becomes safe. Give each container its own config.

An architecture test (`Api.Tests/DependencyInjectionTests/WhenTestingMapsterConfiguration`)
reads the compiled production assemblies — Application, Infrastructure, and the composition
root — and fails on any reference to the static Mapster entry points, and asserts two
containers receive independent configurations. Add a newly covered production assembly to
its `ProductionAssemblies` data when that assembly gains its first `IMapper` usage. Keep it.

The same reasoning applies to any other shared static a test mutates: fix the sharing, do
not serialise the suite around it.

## Validator tests (Application.Tests)

- Separate folder: `{Command}ValidatorTests/` with `Base{Command}ValidatorTest` and
  `WhenTestingValidate.cs`
- Use `_validator.TestValidate(command)` from FluentValidation.TestHelper
- Assert with `ShouldNotHaveAnyValidationErrors()` / `ShouldHaveValidationErrorFor(...)`
- Put every invalid-property and valid-command scenario in `WhenTestingValidate`
- Do **not** add `RuleFor(x => x).NotNull()` on the command — FluentValidation rejects
  `Validate(null)` before that rule runs. Validate nested properties instead.

## API integration tests (Api.Tests)

- Use a `WebApplicationFactory<Program>` subclass. Override `ConfigureWebHost` to:
  - set an environment and in-memory config that supplies an empty connection string,
  - replace real repositories (`RemoveAll<ISystemRepository>()` + add a NSubstitute mock)
    so tests never touch a real database.
- Mock **repositories**, not `IMediator`, so the real handler and application service
  run inside the test host. Do **not** mock `IMediator` merely to verify `Send()`.
- Assert `response.StatusCode` **and** the deserialised body. Cover success and failure
  paths (e.g. healthy record → 200; missing record and repository exception → 503).
- When the endpoint/use case is expected to invoke a substituted repository (or other
  substituted dependency), also verify that call with `Received(n)` / `Arg.Is<>`.
  When it must not (unauthorized, missing required identity claim, mapping failure,
  validation failure, missing prerequisite), use `DidNotReceive()`.
- Group all scenarios for one endpoint/action in `WhenTesting{Action}` (for example
  `WhenTestingGetCurrent`), ordered guard → failure → negative → success.

## Security-token test builders

Builders such as `TestJwt` must represent the **application token contract**, not
whatever an external provider emits by default.

`TestJwt.Email` is present because the FishingLogBook API requires a trusted
authenticated `email` claim in addition to `sub`. Cognito access tokens include
that claim via a Pre Token Generation Lambda (event version V2_0) after a reviewed
Terraform apply. `TestJwt` still represents the application contract, not a raw
Cognito default token.

## Database-backed infrastructure tests

API tests mock repositories and must not require live PostgreSQL in CI.

Normal Infrastructure **unit** tests live at ordinary SUT/feature paths at the
project root (`{Sut}Tests/`). They do not start Docker or PostgreSQL.

Tests that require real external infrastructure live under
`Repositories/`. Use that word, not Integration or Sandbox.

When an issue requires proving uniqueness, transactions, or concurrency, add those
tests in `FishingLogBook.Infrastructure.Tests/Repositories/` against a real
PostgreSQL started by **Testcontainers**. These are automated CI tests on the
GitHub-hosted Ubuntu runner (`ubuntu-latest` / Docker socket). They do **not**
need Neon, a shared CI database, or database connection secrets. Do not add a
workflow Postgres service unless a later issue actually requires one. Do not mock
away the behaviour that must be trusted.

```text
FishingLogBook.Infrastructure.Tests/
    {Sut}Tests/                         → unit tests (no live database)
    Repositories/                       → live-database tests (Testcontainers)
        TestSupport/
            PostgresFixture.cs
            PostgresCollection.cs
        Repositories/
            {Repository}Tests/
                Base{Repository}Test.cs
                WhenTesting{Method}.cs
        Migrations/
            SchemaTests/
        NpgsqlConnectionFactoryTests/
```

`Repositories/` is the live-database category; `Repositories/Repositories/` is the
subfolder for `*Repository` test suites specifically, alongside sibling live-DB areas
that are not themselves repositories (`Migrations/SchemaTests/`,
`NpgsqlConnectionFactoryTests/`). Example:
`Repositories/Repositories/UserIdentityRepositoryTests/`.

Postgres fixtures live only under `Repositories/TestSupport/`. Do not put live-database
repository tests next to unit tests at the project root.

Where an Infrastructure **unit** test has mocked collaborators, apply **Dependency
verification**: assert meaningful `Received(n)` / `DidNotReceive()` and `Arg.Is<>`.
Do **not** force interaction assertions into true integration tests where no
dependency is mocked and the observable persisted/external state is the behaviour
under test.

## DI container tests (Api.Tests)

Unit tests construct services directly, so they will not catch a missing
`AddScoped` / `AddSingleton` in `AddFishingLogBook`. Keep a
`DependencyInjectionTests/` suite that:

- builds the real API host (`WebApplicationFactory<Program>`) with `ValidateOnBuild`
  and `ValidateScopes`,
- reflects endpoint handler methods under `FishingLogBook.Api.Endpoints` and
  `GetRequiredService`s every DI parameter (skip `CancellationToken`, HTTP types,
  primitives, and Shared/Domain bindable DTOs).

This is the safety net for “all tests passed but the app dies on first request”.

## Coverage

Aim for **100% line and branch coverage** of the handler, validator, or endpoint under
test. Do not write filler tests purely to raise a number, and do not widen production
visibility or use reflection just to reach a branch.

## AI feature definition of done (mandatory)

When you add or change production code, add or update tests in the **matching test
project in the same task**.

| Production project | Test project |
|--------------------|--------------|
| `FishingLogBook.Application` | `FishingLogBook.Application.Tests` (handler + validator) |
| `FishingLogBook.Api` | `FishingLogBook.Api.Tests` |
| `FishingLogBook.Infrastructure` | `FishingLogBook.Infrastructure.Tests` |
| `FishingLogBook.Shared` | `FishingLogBook.Shared.Tests` |
| `FishingLogBook.Web` | `FishingLogBook.Web.Tests` — see **`testing-blazor.md`** |

Before marking complete: run targeted `dotnet test` for every test project you changed,
then complete **`self-review.md`** (green tests do not skip that step).

## Before writing new tests

Read at least **5 existing test classes of the same type** (handler, validator, endpoint)
before writing a new one, and match the pattern.
